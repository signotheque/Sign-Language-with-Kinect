﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CURELab.SignLanguage.HandDetector;
using EducationSystem.Detectors;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Controls;
using System.Timers;
using Newtonsoft.Json.Linq;
using Brushes = System.Windows.Media.Brushes;
using ImageConverter = CURELab.SignLanguage.HandDetector.ImageConverter;
using MessageBox = System.Windows.Forms.MessageBox;
using Point = System.Windows.Point;
using SystemColors = System.Windows.SystemColors;
using Timer = System.Timers.Timer;

namespace EducationSystem
{
    enum GuideState
    {
        StartPlay,
        EndPlay,
        StartGuide,
        EndGuide
    }
    /// <summary>
    /// Interaction logic for ShowFeatureMatchedPage.xaml
    /// </summary>
    public partial class ShowFeatureMatchedPage : Page
    {
        private WriteableBitmap _playScreenBitmap;
        public WriteableBitmap PlayScreenBitmap
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get { return _playScreenBitmap; }
            [MethodImpl(MethodImplOptions.Synchronized)]
            set { KinectViewImage.Source = _playScreenBitmap = value; }
        }

        public static readonly DependencyProperty DominantHandPointTopProperty =
            DependencyProperty.Register("DominantHandPointTop", typeof(int), typeof(ShowFeatureMatchedPage), null);

        public int DominantHandPointTop
        {
            get { return (int)(GetValue(DominantHandPointTopProperty)); }
            set { SetValue(DominantHandPointTopProperty, value); }
        }

        public static readonly DependencyProperty DominantHandPointLeftProperty =
            DependencyProperty.Register("DominantHandPointLeft", typeof(int), typeof(ShowFeatureMatchedPage), null);

        public int DominantHandPointLeft
        {
            get { return (int)GetValue(DominantHandPointLeftProperty); }
            set { SetValue(DominantHandPointLeftProperty, value); }
        }

        public static readonly DependencyProperty DotSizeProperty =
            DependencyProperty.Register("DotSize", typeof(int), typeof(ShowFeatureMatchedPage), null);

        public int DotSize
        {
            get { return (int)GetValue(DotSizeProperty); }
            set { SetValue(DotSizeProperty, value); }
        }

        public static readonly DependencyProperty BodyPartProperty =
            DependencyProperty.Register("BodyPart", typeof(string), typeof(ShowFeatureMatchedPage), null);

        public string BodyPart
        {
            get { return (string)GetValue(BodyPartProperty); }
            set { SetValue(BodyPartProperty, value); }
        }

        public static readonly DependencyProperty CurrectWaitingStateProperty =
            DependencyProperty.Register("CurrectWaitingState", typeof(string), typeof(ShowFeatureMatchedPage), null);

        public string CurrectWaitingState
        {
            get { return (string)GetValue(CurrectWaitingStateProperty); }
            set { SetValue(CurrectWaitingStateProperty, value); }
        }

        public static readonly DependencyProperty SignStateProperty =
           DependencyProperty.Register("SignState", typeof(string), typeof(ShowFeatureMatchedPage), null);

        public string SignState
        {
            get { return (string)GetValue(SignStateProperty); }
            set { SetValue(SignStateProperty, value); }
        }

        public static readonly DependencyProperty NumOfFeatureProperty =
            DependencyProperty.Register("NumOfFeature", typeof(int), typeof(ShowFeatureMatchedPage), null);

        public int NumOfFeature
        {
            get { return (int)GetValue(NumOfFeatureProperty); }
            set { SetValue(NumOfFeatureProperty, value); }
        }

        public static readonly DependencyProperty NumOfFeatureCompletedProperty =
    DependencyProperty.Register("NumOfFeatureCompleted", typeof(int), typeof(ShowFeatureMatchedPage), null);

        public int NumOfFeatureCompleted
        {
            get { return (int)GetValue(NumOfFeatureCompletedProperty); }
            set { SetValue(NumOfFeatureCompletedProperty, value); }
        }
        // video controlling
        public static readonly DependencyProperty CurrentFrameBitmapSourceProperty =
    DependencyProperty.Register("CurrentFrameBitmapSource", typeof(BitmapSource), typeof(ShowFeatureMatchedPage), null);

        public BitmapSource CurrentFrameBitmapSource
        {
            get { return (BitmapSource)GetValue(CurrentFrameBitmapSourceProperty); }
            set { SetValue(CurrentFrameBitmapSourceProperty, value); }
        }

        private ShowFeatureMatchedPageFramesHandler framesHandler;
        private VideoModel currentModel;
        private Shape RightSignArrow;
        private Shape LeftSignArrow;
        private SocketManager socket;
        public ShowFeatureMatchedPage()
        {
            InitializeComponent();
        }

        public ObservableCollection<FeatureViewModel> FeatureList { get; set; }

        private ImageViewer viewer;
        private Timer timer_guide;
        private GuideState _state;
        private GuideState State
        {
            get { return _state; }
            set
            {
                _state = value;
                switch (value)
                {
                    case GuideState.StartPlay:
                        btn_Perform.Visibility = Visibility.Collapsed;
                        btn_Replay.Visibility = Visibility.Collapsed;
                        KinectState.Instance.KinectRegion.IsCursorVisible = true;
                        framesHandler.UnregisterCallbacks(KinectState.Instance.CurrentKinectSensor);
                        break;
                    case GuideState.EndPlay:
                        btn_Replay.Visibility = Visibility.Visible;
                        btn_Perform.Visibility = Visibility.Visible;
                        KinectState.Instance.KinectRegion.IsCursorVisible = true;
                        framesHandler.UnregisterCallbacks(KinectState.Instance.CurrentKinectSensor);
                        break;
                    case GuideState.StartGuide:
                        KinectScrollViewer.Visibility = Visibility.Collapsed;
                        img_guide_intersect.Visibility = Visibility.Visible;
                        img_guide_left.Visibility = Visibility.Visible;
                        img_guide_right.Visibility = Visibility.Visible;
                        btn_Perform.Visibility = Visibility.Collapsed;
                        btn_Replay.Visibility = Visibility.Collapsed;
                        KinectState.Instance.KinectRegion.IsCursorVisible = false;
                        framesHandler.RegisterCallbackToSensor(KinectState.Instance.CurrentKinectSensor);
                        break;
                    case GuideState.EndGuide:
                        KinectScrollViewer.Visibility = Visibility.Visible;
                        img_guide_intersect.Visibility = Visibility.Collapsed;
                        img_guide_left.Visibility = Visibility.Collapsed;
                        img_guide_right.Visibility = Visibility.Collapsed;
                        btn_Perform.Visibility = Visibility.Visible;
                        btn_Replay.Visibility = Visibility.Visible;
                        KinectState.Instance.KinectRegion.IsCursorVisible = true;
                        framesHandler.UnregisterCallbacks(KinectState.Instance.CurrentKinectSensor);
                        break;
                    default:
                        break;
                }
            }
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            this.FeatureList = new ObservableCollection<FeatureViewModel>();
            this.FeatureList.Add(new FeatureViewModel("Dominant Hand X", "0"));
            this.FeatureList.Add(new FeatureViewModel("Dominant Hand Y", "0"));
            this.FeatureList.Add(new FeatureViewModel("Region", "0"));
            this.DataContext = this;
            socket = SocketManager.GetInstance();
            DominantHandPointLeft = 300;
            DotSize = 5;

            this.framesHandler = new ShowFeatureMatchedPageFramesHandler(this);
            KinectState.Instance.KinectRegion.IsCursorVisible = true;

            //load signs
            foreach (var row in LearningResource.GetSingleton().VideoModels)
            {
                //load button

                panelSignList.Children.Add(createKinectButton(row));
            }
            KinectScrollViewer.ScrollToVerticalOffset(100);
            viewer = new ImageViewer();
            timer_guide = new Timer()
            {
                Interval = 20
            };
            timer_guide.Elapsed += timer_guide_Elapsed;
            State = GuideState.StartPlay;
            // register data receive event
            socket.DataReceivedEvent += SocketOnDataReceivedEvent;
            // opencv
            //RegisterThreshold("V min", ref OpenCVController.VMIN, 150, OpenCVController.VMIN);
        }

        private void SocketOnDataReceivedEvent(string msg)
        {
            Console.WriteLine(msg);
        }

        //private unsafe void RegisterThreshold(string valuename, ref double thresh, double max, double initialValue)
        //{

        //    fixed (double* ptr = &thresh)
        //    {
        //        thresh = initialValue;
        //        TrackBar tcb = new TrackBar(ptr);
        //        tcb.Max = max;
        //        tcb.Margin = new Thickness(5);
        //        tcb.ValueName = valuename;
        //        initialValue = initialValue > max ? max : initialValue;
        //        tcb.Value = initialValue;
        //        SPn_right.Children.Add(tcb);
        //    }

        //}
        private int GetCurrentFrame()
        {
            int r = 0;
            if (MediaMain.HasVideo)
            {
                r = (int)Math.Round(MediaMain.Position.TotalMilliseconds / 33.333);
            }

            return r;
        }

        private void RemoveArrow(ref Shape s)
        {
            if (BodyPartCanvas.Children.Contains(s))
            {
                BodyPartCanvas.Children.Remove(s);
            }
        }

        private int CurrentKeyFrame = -1;
        void timer_guide_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                if (currentModel != null)
                {
                    var frame = GetCurrentFrame();
                    int startframe = -1;
                    int endframe = -1;
                    for (int i = 0; i < currentModel.KeyFrames.Count; i++)
                    {
                        if (frame >= currentModel.KeyFrames[i].FrameNumber)
                        {
                            startframe = i;
                            endframe = i + 1;
                        }
                    }

                    //if current frame fall in start or end
                    if (startframe == -1 || endframe >= currentModel.KeyFrames.Count)
                    {
                        //remove arrow
                        RemoveArrow(ref RightSignArrow);
                        RemoveArrow(ref LeftSignArrow);
                        // set state
                        SignState = "Finish";
                        NumOfFeatureCompleted = startframe;
                        CurrectWaitingState = "完成";
                        // remove image
                        img_right.Source = null;
                        img_left.Source = null;
                        CurrentKeyFrame = -1;
                        timer_guide.Stop();
                        State = GuideState.EndPlay;
                        return;
                    }
                    else
                    {
                        // set state
                        SignState = currentModel.KeyFrames[endframe].Type.ToString();
                        // draw arrow
                        if (currentModel.KeyFrames[endframe].Type == HandEnum.Both || currentModel.KeyFrames[endframe].Type == HandEnum.Intersect)
                        {
                            MoveArror(ref RightSignArrow, currentModel.KeyFrames[startframe].RightPosition, currentModel.KeyFrames[endframe].RightPosition);
                            MoveArror(ref LeftSignArrow, currentModel.KeyFrames[startframe].LeftPosition, currentModel.KeyFrames[endframe].LeftPosition);
                        }
                        else if (currentModel.KeyFrames[endframe].Type == HandEnum.Right)
                        {
                            MoveArror(ref RightSignArrow, currentModel.KeyFrames[startframe].RightPosition, currentModel.KeyFrames[endframe].RightPosition);
                            RemoveArrow(ref LeftSignArrow);
                        }

                        // image
                        if (currentModel.KeyFrames[endframe].Type == HandEnum.Intersect)
                        {
                            img_intersect.Source = currentModel.KeyFrames[endframe].RightImage;
                            img_right.Source = null;
                            img_left.Source = null;
                        }
                        else if (currentModel.KeyFrames[endframe].Type == HandEnum.Both)
                        {
                            img_right.Source = currentModel.KeyFrames[endframe].RightImage;
                            img_left.Source = currentModel.KeyFrames[endframe].LeftImage;
                            img_intersect.Source = null;
                        }
                        else if (currentModel.KeyFrames[endframe].Type == HandEnum.Right)
                        {
                            img_right.Source = currentModel.KeyFrames[endframe].RightImage;
                            img_left.Source = null;
                            img_intersect.Source = null;
                        }
                    }
                    if (CurrentKeyFrame != startframe)
                    {
                        CurrentKeyFrame = startframe;
                        NumOfFeatureCompleted = CurrentKeyFrame;
                        CurrectWaitingState = "第" + (NumOfFeatureCompleted + 1) + "步";
                        switch (currentModel.KeyFrames[endframe].Type)
                        {
                            case HandEnum.Both:
                                CurrectWaitingState += "分別移動你的【左右手】到箭頭所示位置，并做出相應手勢";
                                break;
                            case HandEnum.Intersect:
                                CurrectWaitingState += "移動你的【雙手】到箭頭所示位置，并做出相應手勢";
                                break;
                            case HandEnum.Right:
                                CurrectWaitingState += "移動你的【右手】到箭頭所示位置，并做出相應手勢";
                                break;
                        }
                        MediaMain.Pause();
                        timer_guide.Stop();
                        new Thread(() =>
                        {
                            Thread.Sleep(5000);
                            timer_guide.Start();
                            Application.Current.Dispatcher.Invoke(() => MediaMain.Play());
                        }).Start();

                    }
                }
            }));

        }

        private static Shape DrawLinkArrow(Point p1, Point p2)
        {
            p1 = p1 * new Matrix(0.75, 0, 0, 0.75, 0, 0);
            p2 = p2 * new Matrix(0.75, 0, 0, 0.75, 0, 0);
            GeometryGroup lineGroup = new GeometryGroup();
            double theta = Math.Atan2((p2.Y - p1.Y), (p2.X - p1.X)) * 180 / Math.PI;

            //PathGeometry pathGeometry = new PathGeometry();
            //PathFigure pathFigure = new PathFigure();
            //            Point p = new Point(p1.X + ((p2.X - p1.X) / 1.35), p1.Y + ((p2.Y - p1.Y) / 1.35));
            Point p = p2;
            //pathFigure.StartPoint = p;

            Point lpoint = new Point(p.X + 6, p.Y + 15);
            Point rpoint = new Point(p.X - 6, p.Y + 15);
            //LineSegment seg1 = new LineSegment();
            //seg1.Point = lpoint;
            //pathFigure.Segments.Add(seg1);

            //LineSegment seg2 = new LineSegment();
            //seg2.Point = rpoint;
            //pathFigure.Segments.Add(seg2);

            //LineSegment seg3 = new LineSegment();
            //seg3.Point = p;
            //pathFigure.Segments.Add(seg3);

            RotateTransform transform = new RotateTransform();
            transform.Angle = theta + 90;
            transform.CenterX = p.X;
            transform.CenterY = p.Y;

            LineGeometry lGeometry = new LineGeometry();
            lGeometry.StartPoint = p;
            lGeometry.EndPoint = lpoint;
            lGeometry.Transform = transform;
            lineGroup.Children.Add(lGeometry);
            LineGeometry rGeometry = new LineGeometry();
            rGeometry.StartPoint = p;
            rGeometry.EndPoint = rpoint;
            rGeometry.Transform = transform;
            lineGroup.Children.Add(rGeometry);

            //pathGeometry.Figures.Add(pathFigure);


            //pathGeometry.Transform = transform;
            //lineGroup.Children.Add(pathGeometry);


            LineGeometry connectorGeometry = new LineGeometry();
            connectorGeometry.StartPoint = p1;
            connectorGeometry.EndPoint = p2;
            lineGroup.Children.Add(connectorGeometry);
            Path path = new Path { Data = lineGroup, StrokeThickness = 2 };
            path.Stroke = path.Fill = Brushes.Black;
            path.Opacity = 0.8;
            return path;
        }

        private KinectTileButton createKinectButton(VideoModel dc)
        {
            KinectTileButton button = new KinectTileButton();
            button.DataContext = dc;
            button.Click += btnSignWord_Click;
            button.Content = dc.Chinese;
            button.Width = 250;
            button.Height = 110;
            button.FontSize = 48;
            SolidColorBrush brush = new SolidColorBrush(Brushes.Aqua.Color);
            brush.Opacity = 0.2;
            button.Background = brush;
            return button;
        }


        private void btnSignWord_Click(object sender, RoutedEventArgs e)
        {
            KinectTileButton button = (KinectTileButton)sender;
            var dc = button.DataContext as VideoModel;
            //string videoName = String.Format("Videos\\{0}.mpg", button.DataContext.ToString());
            //viewer.Show();
            //Thread t = new Thread(new ParameterizedThreadStart(PlayVideo));
            //t.Start(dc.Path);
            currentModel = dc;
            if (dc.KeyFrames.Count > 0)
            {
                MediaMain.Source = new Uri(dc.Path);
                CurrentKeyFrame = -1;
                NumOfFeature = dc.KeyFrames.Count - 1;
                NumOfFeatureCompleted = 0;
                State = GuideState.StartPlay;
                timer_guide.Start();
            }
            else
            {
                MessageBox.Show("This word is not prepared");
            }


        }
        Capture _CCapture = null;
        private void PlayVideo(object file)
        {
            try
            {
                if (_CCapture != null)
                {
                    _CCapture.Dispose(); //dispose of current capture
                }
                _CCapture = new Capture(file as string);
                int FrameRate = (int)_CCapture.GetCaptureProperty(Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_FPS);
                int cframe = (int)_CCapture.GetCaptureProperty(Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_POS_FRAMES);
                int framenumber = (int)_CCapture.GetCaptureProperty(Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_FRAME_COUNT);
                while (_CCapture.Grab())
                {
                    var frame = _CCapture.RetrieveBgrFrame().Resize(800, 600, INTER.CV_INTER_LINEAR);
                    System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)delegate()
                    {
                        viewer.Size = frame.Size;
                        viewer.Image = frame;
                    });
                    Thread.Sleep(1000 / FrameRate);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)delegate()
                {
                    viewer.Hide();
                });
            }

        }

      

        private void drawRegionOnCanvas(BodyPart bodyPart)
        {

        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                DominantHandPointLeft = int.Parse((sender as TextBox).Text);
            }
            catch (Exception)
            {

            }

        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            try
            {
                DominantHandPointTop = int.Parse((sender as TextBox).Text);
            }
            catch (Exception)
            {

            }
        }

        private void TextBox_TextChanged_2(object sender, TextChangedEventArgs e)
        {

        }

        private void MoveArror(ref Shape s, Point from, Point to)
        {
            if (s != null)
            {
                RemoveArrow(ref s);
            }
            s = DrawLinkArrow(from, to);
            BodyPartCanvas.Children.Add(s);
        }



        private void MediaMain_OnLoaded(object sender, RoutedEventArgs e)
        {
            var s = sender as MediaElement;
            s.Play();
            s.Pause();
        }

        private void Btn_Replay_OnClick(object sender, RoutedEventArgs e)
        {
            CurrentKeyFrame = -1;
            NumOfFeatureCompleted = 0;
            State = GuideState.StartPlay;
            MediaMain.Position = TimeSpan.FromSeconds(0);
            timer_guide.Start();
        }

        private void Btn_Perform_OnClick(object sender, RoutedEventArgs e)
        {
            State = GuideState.StartGuide;
            var j = new JObject();
            j["label"] = "guide";
            j["wordname"] = currentModel.ID;
            socket.SendDataAsync(j.ToString());
        }

        private class ShowFeatureMatchedPageFramesHandler : AbstractKinectFramesHandler
        {
            private BodyPartDetector detector = new BodyPartDetector();
            private PrickSignDetector prickSignDetector;
            private ShowFeatureMatchedPage showFeatureMatchedPage;
            private ReaderWriterLockSlim frameLock;
            private bool isRightHandPrimary = true;
            private Skeleton skeleton;
            private byte[] colorPixels;
            private DepthImagePoint[] depthMap;
            private DepthImagePixel[] depthPixels;
            private System.Drawing.Point headPosition;
            private int headDepth;

            public ShowFeatureMatchedPageFramesHandler(ShowFeatureMatchedPage showFeatureMatchedPage)
            {
                this.showFeatureMatchedPage = showFeatureMatchedPage;
                this.frameLock = new ReaderWriterLockSlim();
                this.prickSignDetector = new PrickSignDetector(showFeatureMatchedPage);
                OpenCVController.GetSingletonInstance().StartDebug();
                
            }

            private HandShapeModel GenerateTest(Skeleton skl)
            {
                var model = new HandShapeModel(HandEnum.Right);
                model.right = new System.Drawing.Rectangle(0, 0, 0, 0);
                model.left = new System.Drawing.Rectangle(0, 0, 0, 0);
                model.skeletonData = FrameConverter.GetFrameDataArgString(sensor, skl);
                return model;
            }

            public override void DepthFrameCallback(long timestamp, int frameNumber, DepthImagePixel[] depthPixel)
            {
                if (depthMap == null)
                {
                    depthMap = new DepthImagePoint[sensor.DepthStream.FramePixelDataLength];
                }
                this.depthPixels = depthPixel;
                sensor.CoordinateMapper.MapColorFrameToDepthFrame(
                             sensor.ColorStream.Format, sensor.DepthStream.Format,
                             depthPixel,
                             this.depthMap);
            }

            public override void SkeletonFrameCallback(long timestamp, int frameNumber, Skeleton[] skeletonData)
            {
                bool isTracked = false;
                Tuple<BodyPart, BodyPart> bodyPartForHands = null;
                Point relativePosition = new Point();
                bool leftHandRaise = false;

                foreach (Skeleton skeleton in skeletonData)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        isTracked = true;
                        //bodyPartForHands = detector.decide(skeleton);
                        //prickSignDetector.Update(skeleton);

                        Joint hand1 = skeleton.Joints[isRightHandPrimary ? JointType.HandRight : JointType.HandLeft];
                        Joint hand2 = skeleton.Joints[isRightHandPrimary ? JointType.HandLeft : JointType.HandRight];
                        Joint shoulderLeft = skeleton.Joints[JointType.ShoulderLeft];
                        Joint shoulderCenter = skeleton.Joints[JointType.ShoulderCenter];
                        Joint shoulderRight = skeleton.Joints[JointType.ShoulderRight];
                        if (skeleton.Joints[JointType.Head].TrackingState == JointTrackingState.Tracked)
                        {
                            SkeletonPoint head = skeleton.Joints[JointType.Head].Position;
                            headPosition = SkeletonPointToScreen(head);
                            try
                            {
                                headDepth = depthPixels[headPosition.X + headPosition.Y * 640].Depth;
                            }
                            catch (Exception)
                            {
                                Console.WriteLine(headPosition.X);
                                Console.WriteLine(headPosition.Y);
                                Console.WriteLine(headPosition.X + headPosition.Y * 640);
                                return;
                            }
                        }
                        if (hand2.Position.Y > skeleton.Joints[JointType.HipCenter].Position.Y - 0.12)
                        {
                            leftHandRaise = true;
                        }

                        if (hand1.Position.X > shoulderCenter.Position.X)
                        {
                            relativePosition.X = (hand1.Position.X - shoulderCenter.Position.X) / (shoulderRight.Position.X - shoulderCenter.Position.X);
                        }
                        else
                        {
                            relativePosition.X = -(hand1.Position.X - shoulderCenter.Position.X) / (shoulderLeft.Position.X - shoulderCenter.Position.X);
                        }

                        relativePosition.Y = 0;

                        var handModel = OpenCVController.GetSingletonInstance()
                            .FindHandFromColor(null, colorPixels, depthMap, headPosition, headDepth, 4);
                        if (handModel != null && handModel.type != HandEnum.None)
                        {
                            if (handModel.intersectCenter != System.Drawing.Rectangle.Empty
                                && !leftHandRaise)
                            {
                                //false intersect right hand behind head and left hand on initial position
                                // to overcome the problem of right hand lost and left hand recognized as intersected.
                            }
                            else
                            {
                                if (!leftHandRaise && handModel.type == HandEnum.Both)
                                {
                                    handModel.type = HandEnum.Right;
                                }
                                Console.WriteLine(handModel.type);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    switch (handModel.type)
                                    {
                                        case HandEnum.Right:
                                            showFeatureMatchedPage.img_guide_right.Source = handModel.RightColor.Bitmap.ToBitmapSource();
                                            break;
                                        case HandEnum.Both:
                                            showFeatureMatchedPage.img_guide_right.Source = handModel.RightColor.Bitmap.ToBitmapSource();
                                            showFeatureMatchedPage.img_guide_left.Source = handModel.LeftColor.Bitmap.ToBitmapSource();
                                            break;
                                        case HandEnum.Intersect:
                                            showFeatureMatchedPage.img_guide_intersect.Source = handModel.RightColor.Bitmap.ToBitmapSource();
                                            break;
                                        default:
                                            return;
                                    }
                                });
                                
                                SocketManager.GetInstance().SendDataAsync(handModel);
                            }
                        }
                        Console.WriteLine("tracked");
                        //foreach (FeatureViewModel viewModel in showFeatureMatchedPage.FeatureList)
                        //{
                        //    if ("Dominant Hand X".Equals(viewModel.FeatureName))
                        //    {
                        //        viewModel.Value = relativePosition.X.ToString();
                        //    }
                        //    else if ("Dominant Hand Y".Equals(viewModel.FeatureName))
                        //    {
                        //        viewModel.Value = relativePosition.Y.ToString();
                        //    }
                        //    else if ("Region".Equals(viewModel.FeatureName))
                        //    {
                        //        viewModel.Value = bodyPartForHands.Item1.ToString();
                        //    }
                        //}
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (isTracked && bodyPartForHands != null)
                    {
                        showFeatureMatchedPage.DominantHandPointLeft = 421 + (int)(relativePosition.X * (467 - 375) / 2);
                        showFeatureMatchedPage.DominantHandPointTop = 135 - (int)(relativePosition.Y * (135 - 65) / 2);
                        showFeatureMatchedPage.FeatureDataGrid.Items.Refresh();
                        showFeatureMatchedPage.BodyPart = bodyPartForHands.Item1.ToString();
                    }
                    else
                    {
                        showFeatureMatchedPage.BodyPart = "";
                    }
                });
            }

            public override void ColorFrameCallback(long timestamp, int frameNumber, byte[] colorPixels)
            {
                if (colorPixels != null && colorPixels.Length > 0)
                {
                    frameLock.EnterWriteLock();
                    this.colorPixels = colorPixels;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (showFeatureMatchedPage.PlayScreenBitmap == null)
                        {
                            showFeatureMatchedPage.PlayScreenBitmap = new WriteableBitmap(640, 480, 96.0, 96.0, PixelFormats.Bgr32, null);
                        }

                        showFeatureMatchedPage.PlayScreenBitmap.WritePixels(new Int32Rect(0, 0, 640, 480), colorPixels, 640 * sizeof(int), 0);
                    });
                    frameLock.ExitWriteLock();
                }
            }
        }

    }

}
