' The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

Imports Windows.UI
Imports Windows.UI.Xaml.Controls
Imports Microsoft.Graphics.Canvas
Imports Windows.Graphics.Imaging
Imports Windows.Storage.Streams
Imports Windows.Storage
Imports Windows
Imports Windows.Media.Capture
Imports Windows.Media.Devices
Imports Windows.Devices.Enumeration
Imports Windows.System.Display
Imports Windows.Foundation.Metadata
Imports Windows.Media.MediaProperties
Imports Windows.UI.Xaml.Shapes
Imports Windows.UI.Core
Imports Windows.Storage.FileProperties

Imports Microsoft.Graphics.Canvas.UI.Xaml

''' <summary>
''' An empty page that can be used on its own or navigated to within a Frame.
''' </summary>
Public NotInheritable Class MainPage
    Inherits Page
    Private canDevice As CanvasDevice
    Private _mediaCapture As MediaCapture
    Private _tolerance As Double = 0.1

    Private _startPoint As Input.PointerPoint
    Private _endPoint As Input.PointerPoint
    Private _boxPoint As Input.PointerPoint

    Private ReadOnly _displayRequest As DisplayRequest = New DisplayRequest()


    Private _flipX As Boolean = False
    Private _flipY As Boolean = False
    Private _opacity As Integer = 1
    Private _vpIndex As Integer = 0
    Private _settings As ApplicationDataContainer = ApplicationData.Current.LocalSettings
    Private _bInitialized As Boolean = False
    Private _IsDrawing As Boolean = False
    Private _EncodingProperties As StreamResolution
    Private _rotationHelper As CameraRotationHelper


    ' Folder in which the captures will be stored (initialized in SetupUiAsync)
    'Private _captureFolder As StorageFolder = Nothing


    Private Async Sub Application_Resuming(sender As Object, o As Object)
        If Frame.CurrentSourcePageType Is GetType(MainPage) Then

            Await InitializeCamera()
        End If
    End Sub



    Public Async Function InitializeCamera() As Task

        Dim cameraDevice = Await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Unknown)
        If cameraDevice Is Nothing Then
            Debug.WriteLine("No camera device found!")
            Return
        End If

        _mediaCapture = New MediaCapture()
        'AddHandler _mediaCapture.Failed, AddressOf MediaCapture_Failed

        Dim settings = New MediaCaptureInitializationSettings With {.VideoDeviceId = cameraDevice.Id}


        Try
            Await _mediaCapture.InitializeAsync(settings)

        Catch ex As UnauthorizedAccessException
            Debug.WriteLine("The app was denied access to the camera")
        End Try

        ' Initialize rotationHelper
        _rotationHelper = New CameraRotationHelper(cameraDevice.EnclosureLocation)
        'AddHandler _rotationHelper.OrientationChanged, AddressOf RotationHelper_OrientationChanged

        Await StartPreviewAsync()




    End Function




    ''' <summary>
    ''' Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on
    ''' </summary>
    ''' <returns></returns>
    Private Async Function StartPreviewAsync() As Task
        _displayRequest.RequestActive()
        PreviewControl.Source = _mediaCapture

        SetRotation()
        PopulateVideoSettingComboBox()


        Await _mediaCapture.StartPreviewAsync()

        Dim props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview)


        Await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, Nothing)

        CboVideoSettings.SelectedIndex = _vpIndex

        'Await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, encprop)


    End Function


    ''' <summary>
    ''' Attempts to find and return a device mounted on the panel specified, and on failure to find one it will return the first device listed
    ''' </summary>
    ''' <param name="desiredPanel">The desired panel on which the returned device should be mounted, if available</param>
    ''' <returns></returns>
    Private Shared Async Function FindCameraDeviceByPanelAsync(desiredPanel As Windows.Devices.Enumeration.Panel) As Task(Of DeviceInformation)
        ' Get available devices for capturing pictures
        Dim allVideoDevices = Await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)
        ' Get the desired camera by panel
        Dim desiredDevice As DeviceInformation = allVideoDevices.FirstOrDefault(Function(x) x.EnclosureLocation IsNot Nothing AndAlso x.EnclosureLocation.Panel = desiredPanel)
        Return If(desiredDevice, allVideoDevices.FirstOrDefault())
    End Function

    Private Sub SetRotation()

        Dim t As New ScaleTransform

        t.ScaleX = If(_flipX, -1, 1)

        t.ScaleY = If(_flipY, -1, 1)


        ImgOverlay.RenderTransform = t

        PreviewControl.RenderTransform = t
        'dpCanvas.RenderTransform = t

    End Sub
    Private Async Sub PhotoButton_Click(sender As Object, e As RoutedEventArgs)


        Dim stream = New InMemoryRandomAccessStream()
        Debug.WriteLine("Taking photo...")
        Await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreatePng(), stream)


        canDevice = CanvasDevice.GetSharedDevice()


        Dim bitm As CanvasBitmap = Await CanvasBitmap.LoadAsync(canDevice, stream)


        Dim img As CanvasImageSource = New CanvasImageSource(canDevice, bitm.Bounds.Width, bitm.Bounds.Height, 96)

        Dim ds As CanvasDrawingSession = img.CreateDrawingSession(Colors.Transparent)

        Dim r As Rect

        If _startPoint Is Nothing Or _endPoint Is Nothing Then

            Dim w As Integer = bitm.Bounds.Width \ 2
            Dim h As Integer = bitm.Bounds.Height \ 2


            r = New Rect(w - 250, h - 200, 500, 400)
        Else

            Dim p1 As Point
            Dim p2 As Point

            Dim xf As Double = PreviewControl.ActualWidth / bitm.Size.Width
            Dim yf As Double = PreviewControl.ActualHeight / bitm.Size.Height


            p1.X = ds.ConvertDipsToPixels(_startPoint.Position.X, CanvasDpiRounding.Ceiling) / xf

            p1.Y = ds.ConvertDipsToPixels(_startPoint.Position.Y, CanvasDpiRounding.Ceiling) / yf

            p2.X = ds.ConvertDipsToPixels(_endPoint.Position.X, CanvasDpiRounding.Ceiling) / xf
            p2.Y = ds.ConvertDipsToPixels(_endPoint.Position.Y, CanvasDpiRounding.Ceiling) / yf

            r = New Rect(p1, p2)

        End If


        Dim crop As New Effects.CropEffect
        With crop
            .Source = bitm
            .SourceRectangle = r

        End With
        Dim chrome As New Effects.ChromaKeyEffect()
        With chrome
            .Color = Colors.White
            .Feather = True
            .Tolerance = _tolerance
            .Source = crop

        End With

        ds.DrawImage(chrome)
        ds.Dispose()

        ImgOverlay.Source = img

        If DrawingPanel.Children.Count = 1 Then
            DrawingPanel.Children.ElementAt(0).Visibility = Visibility.Collapsed
        End If

    End Sub





    Private Shared Async Function ReencodeAndSavePhotoAsync(stream As IRandomAccessStream, file As StorageFile, photoOrientation As PhotoOrientation) As Task
        Using inputStream = stream
            Dim decoder = Await BitmapDecoder.CreateAsync(inputStream)
            Using outputStream = Await file.OpenAsync(FileAccessMode.ReadWrite)
                Dim encoder = Await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder)
                'Dim properties = New BitmapPropertySet From {{"System.Photo.Orientation", New BitmapTypedValue(photoOrientation, PropertyType.UInt16)}}
                'Await encoder.BitmapProperties.SetPropertiesAsync(properties)
                Await encoder.FlushAsync()
            End Using
        End Using
    End Function

    Private Sub ClearButton_Click(sender As Object, e As RoutedEventArgs) Handles ClearButton.Click
        ImgOverlay.Source = Nothing
        If DrawingPanel.Children.Count = 1 Then
            DrawingPanel.Children.ElementAt(0).Visibility = Visibility.Visible
        End If


    End Sub



    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)

        _flipX = If(_settings.Values.ContainsKey("FlipX"), _settings.Values("FlipX"), False)
        _flipY = If(_settings.Values.ContainsKey("FlipY"), _settings.Values("FlipY"), False)
        _tolerance = If(_settings.Values.ContainsKey("Tolerance"), _settings.Values("Tolerance"), 0.25)
        _opacity = If(_settings.Values.ContainsKey("Opacity"), _settings.Values("Opacity"), 100)
        _vpIndex = If(_settings.Values.ContainsKey("encoding"), _settings.Values("encoding"), 0)

        sdrOpacity.Value = _opacity
        sdrTolerance.Value = _tolerance * 100
        chkFlipX.IsChecked = _flipX
        chkFlipY.IsChecked = _flipY

        _bInitialized = True
        Await InitializeCamera()
    End Sub

    Private Sub PopulateVideoSettingComboBox()
        Dim allProperties As IEnumerable(Of StreamResolution) = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).Select(Function(x)
                                                                                                                                                                               Return New StreamResolution(x)
                                                                                                                                                                           End Function)
        allProperties = allProperties.OrderByDescending(
                                                        Function(x)
                                                            Return x.Height * x.Width
                                                        End Function).ThenByDescending(
                                                                                        Function(x)
                                                                                            Return x.FrameRate
                                                                                        End Function)

        For Each _property In allProperties
            If _property.FrameRate < 31 And _property.FrameRate > 14 Then
                Dim ComboBoxItem = New ComboBoxItem()
                ComboBoxItem.Content = _property.GetFriendlyName()
                ComboBoxItem.Tag = _property
                CboVideoSettings.Items.Add(ComboBoxItem)
            End If

        Next

    End Sub



    Private Sub CleanupCamera()
        Debug.WriteLine("CleanupCamera")


        If _mediaCapture IsNot Nothing Then
            _mediaCapture.Dispose()
            _mediaCapture = Nothing
        End If

    End Sub


    Private Sub sdrOpacity_ValueChanged(sender As Object, e As RangeBaseValueChangedEventArgs) Handles sdrOpacity.ValueChanged
        If _bInitialized Then _settings.Values("Opacity") = e.NewValue

        ImgOverlay.Opacity = e.NewValue / 100

    End Sub

    Private Sub sdrTolerance_ValueChanged(sender As Object, e As RangeBaseValueChangedEventArgs) Handles sdrTolerance.ValueChanged
        '
        _tolerance = e.NewValue / 100
        _settings.Values("Tolerance") = _tolerance
        'TODO: ?? call procedure to snap new photo ??
    End Sub




    Private Sub chkFlipX_Click(sender As Object, e As RoutedEventArgs) Handles chkFlipX.Click
        _flipX = chkFlipX.IsChecked
        _settings.Values("FlipX") = _flipX
        DrawingPanel.Children.Clear()

        SetRotation()

    End Sub

    Private Sub chkFlipY_Click(sender As Object, e As RoutedEventArgs) Handles chkFlipY.Click
        _flipY = chkFlipY.IsChecked
        _settings.Values("FlipY") = _flipY
        DrawingPanel.Children.Clear()
        SetRotation()
    End Sub

    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        CleanupCamera()
    End Sub

    Private Sub PreviewControl_PointerPressed(sender As Object, e As PointerRoutedEventArgs) Handles PreviewControl.PointerPressed
        _startPoint = e.GetCurrentPoint(PreviewControl)
        _boxPoint = e.GetCurrentPoint(Nothing)
        _IsDrawing = True
    End Sub

    Private Sub PreviewControl_PointerReleased(sender As Object, e As PointerRoutedEventArgs) Handles PreviewControl.PointerReleased
        _endPoint = e.GetCurrentPoint(PreviewControl)
        _IsDrawing = False
    End Sub
    Private Sub PreviewControl_PointerMoved(sender As Object, e As PointerRoutedEventArgs) Handles PreviewControl.PointerMoved
        DrawCaptureRegion(e.GetCurrentPoint(Nothing).Position)
    End Sub


    Private Sub DrawCaptureRegion(p As Point)
        If _IsDrawing Then
            Dim r As Rect
            Dim captureRegion As Rectangle = New Rectangle


            DrawingPanel.Children.Clear()

            r = New Rect(_boxPoint.Position, p)

            captureRegion.Width = r.Width
            captureRegion.Height = r.Height

            Canvas.SetLeft(captureRegion, r.Left)

            Canvas.SetTop(captureRegion, r.Top)

            captureRegion.StrokeThickness = 2
            captureRegion.Stroke = New SolidColorBrush(Colors.Red)
            DrawingPanel.Children.Add(captureRegion)


        End If
    End Sub

    Private Sub MainPage_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles Me.SizeChanged
        'Dim ratio As Double = If(_EncodingProperties Is Nothing, 1.78, _EncodingProperties.AspectRatio)


        'PreviewControl.Width = e.NewSize.Width
        'PreviewControl.Height = e.NewSize.Width / ratio
        'ImgOverlay.Width = e.NewSize.Width
        'ImgOverlay.Height = e.NewSize.Width / ratio
        'dpCanvas.Width = e.NewSize.Width
        'dpCanvas.Height = e.NewSize.Width / ratio

        ChangeSize()

    End Sub

    Private Async Sub CboVideoSettings_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles CboVideoSettings.SelectionChanged
        Dim cb As ComboBox = DirectCast(sender, ComboBox)
        Dim selectedItem As ComboBoxItem = cb.SelectedItem


        _EncodingProperties = DirectCast(selectedItem.Tag, StreamResolution)
        Await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, _EncodingProperties.EncodingProperties)

        _settings.Values("encoding") = cb.SelectedIndex

        ChangeSize()
    End Sub
    Private Sub ChangeSize()
        Dim ratio As Double = If(_EncodingProperties Is Nothing, 1.78, _EncodingProperties.AspectRatio)
        Dim h As Double = MainGridRow0.ActualHeight
        Dim w As Double = MainGridCol0.ActualWidth

        Dim newSize = If(h * ratio > w, w / ratio, h)

        PreviewControl.Width = newSize * ratio
        ImgOverlay.Width = newSize * ratio
            dpCanvas.Width = newSize * ratio

            PreviewControl.Height = newSize
            ImgOverlay.Height = newSize
            dpCanvas.Height = newSize


    End Sub
End Class
