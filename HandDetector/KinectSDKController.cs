﻿// author：      Administrator
// created time：2014/1/14 15:59:58
// organizatioin:CURE lab, CUHK
// copyright：   2014-2015
// CLR：         4.0.30319.18052
// project link：https://github.com/huangfuyang/Sign-Language-with-Kinect

using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

using CURELab.SignLanguage.StaticTools;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

using CURELab.SignLanguage.HandDetector;
using System.Diagnostics;

namespace CURELab.SignLanguage.HandDetector
{

    /// <summary>
    /// Kinect SDK controller
    /// </summary>
    public class KinectSDKController : KinectController
    {

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;


        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        private Colorizer colorizer;

        private Skeleton[] skeletons;

        private System.Drawing.Point rightHandPosition;
        private System.Drawing.Point headPosition;

        public static double CullingThresh;
        public static float AngleRotateTan = AaronRotateTan;
        // demo
        public const float DemoRotateTan = 0.45f;
        // anita
        public const float AnitaRotateTan = 0.3f;
        // michael
        public const float MichaelRotateTan = 0.23f;
        // Aaron
        public const float AaronRotateTan = 0.28f;

        const int handShapeWidth = 60;
        const int handShapeHeight = 60;

        private KinectSDKController()
            : base()
        {
            KinectSensor.KinectSensors.StatusChanged += Kinect_StatusChanged;
        }

        public static KinectController GetSingletonInstance()
        {
            if (singleInstance == null)
            {
                singleInstance = new KinectSDKController();
            }
            return singleInstance;
        }

        public override void Initialize(string uri = null)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            ConsoleManager.Show();

            if (null != this.sensor)
            {
                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                this.sensor.SkeletonStream.Enable();
                //this.sensor.DepthStream.Range = DepthRange.Near;
                // Allocate space to put the pixels we'll receive           
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.ColorWriteBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, System.Windows.Media.PixelFormats.Bgr32, null);
                this.DepthWriteBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, System.Windows.Media.PixelFormats.Bgr32, null);
                this.WrtBMP_RightHandFront = new WriteableBitmap(handShapeWidth, handShapeHeight, 96.0, 96.0, System.Windows.Media.PixelFormats.Gray8, null);
                this.WrtBMP_LeftHandFront = new WriteableBitmap(handShapeWidth, handShapeHeight, 96.0, 96.0, System.Windows.Media.PixelFormats.Gray8, null);
                WrtBMP_Candidate1 = new WriteableBitmap(handShapeWidth, handShapeHeight, 96.0, 96.0, System.Windows.Media.PixelFormats.Gray8, null);
                WrtBMP_Candidate2 = new WriteableBitmap(handShapeWidth, handShapeHeight, 96.0, 96.0, System.Windows.Media.PixelFormats.Gray8, null);
                WrtBMP_Candidate3 = new WriteableBitmap(handShapeWidth, handShapeHeight, 96.0, 96.0, System.Windows.Media.PixelFormats.Gray8, null);
                // Add an event handler to be called whenever there is new frame data
                this.sensor.AllFramesReady += this.AllFrameReady;
                this.Status = Properties.Resources.Connected;

                this.colorizer = new Colorizer();
                rightHandPosition = new System.Drawing.Point();
            }

            if (null == this.sensor)
            {
                this.Status = Properties.Resources.NoKinectReady;
            }

        }


        private void Kinect_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Connected:
                    if (sensor == null)
                    {
                        sensor = e.Sensor;
                        Initialize();
                        Start();
                    }
                    break;
                case KinectStatus.Disconnected:
                    if (sensor == e.Sensor)
                    {

                        this.Status = Properties.Resources.NoKinectReady;
                        // Notify user, change state of APP appropriately
                    }
                    break;
                case KinectStatus.NotReady:
                    break;
                case KinectStatus.NotPowered:
                    if (sensor == e.Sensor)
                    {
                        this.Status = Properties.Resources.NoKinectReady;
                        // Notify user, change state of APP appropriately
                    }
                    break;
                default:
                    // Throw exception, notify user or ignore depending on use case
                    break;
            }
        }

        private void ProcessOneFrame()
        {
                        
        }
        short headDepth = 0;
        private void AllFrameReady(object sender, AllFramesReadyEventArgs e)
        {
            //Console.Clear();
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(this.skeletons);
                    Skeleton skel = skeletons[0];
                    if (skel.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        SkeletonPoint rightHand = skeletons[0].Joints[JointType.HandRight].Position;
                        SkeletonPoint head = skeletons[0].Joints[JointType.Head].Position;
                        rightHandPosition = SkeletonPointToScreen(rightHand);
                        headPosition = SkeletonPointToScreen(head);

                    }
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.ColorWriteBitmap.WritePixels(
                        new System.Windows.Int32Rect(0, 0, this.ColorWriteBitmap.PixelWidth, this.ColorWriteBitmap.PixelHeight),
                        this.colorPixels,
                        this.ColorWriteBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    var sw = Stopwatch.StartNew();
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);
                    // short[] depthData = new short[depthFrame.PixelDataLength];
                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;
                    int width = depthFrame.Width;
                    int height = depthFrame.Height;

                    if (headPosition.X == 0)
                    {
                        headDepth = 100;
                    }
                    else
                    {
                        headDepth = depthPixels[headPosition.X + headPosition.Y * 640].Depth;
                    }

                    sw.Restart();
                    //*********** Convert cull and transform*****************
                    colorizer.TransformCullAndConvertDepthFrame(
                        depthPixels, minDepth, maxDepth, colorPixels,
                        AngleRotateTan,
                        (short)(headDepth - (short)CullingThresh), headPosition);

                    Image<Bgra, byte> depthImg;
                    //Console.WriteLine("iteration:" + sw.ElapsedMilliseconds);
                    sw.Restart();


                    Image<Gray, Byte> rightFront = null;
                    Image<Gray, Byte> leftFront = null;
                    depthImg = ImageConverter.Array2Image(colorPixels, width, height, width * 4);
                    PointF rightVector = PointF.Empty;
                    PointF leftVector = PointF.Empty;
                    bool isSkip = false;
                    bool leftHandRaise = false;
                    if (skeletons != null && skeletons[0].TrackingState == SkeletonTrackingState.Tracked)
                    {
                        PointF hr = SkeletonPointToScreen(skeletons[0].Joints[JointType.HandRight].Position);
                        PointF hl = SkeletonPointToScreen(skeletons[0].Joints[JointType.HandLeft].Position);
                        PointF er = SkeletonPointToScreen(skeletons[0].Joints[JointType.ElbowRight].Position);
                        PointF el = SkeletonPointToScreen(skeletons[0].Joints[JointType.ElbowLeft].Position);
                        PointF hip = SkeletonPointToScreen(skeletons[0].Joints[JointType.HipCenter].Position);
                        // hand is lower than hip
                        //Console.WriteLine(skeletons[0].Joints[JointType.HandRight].Position.Y);
                        //Console.WriteLine(skeletons[0].Joints[JointType.HipCenter].Position.Y);
                        //Console.WriteLine("-------------");
                        if (skeletons[0].Joints[JointType.HandRight].Position.Y <
                            skeletons[0].Joints[JointType.HipCenter].Position.Y + 0.05)
                        {
                            isSkip = true;
                        }
                        if (skeletons[0].Joints[JointType.HandLeft].Position.Y >
                            skeletons[0].Joints[JointType.HipCenter].Position.Y )
                        {
                            leftHandRaise = true;
                        }
                       
                        rightVector.X = (hr.X - er.X);
                        rightVector.Y = (hr.Y - er.Y);
                        leftVector.X = (hl.X - el.X);
                        leftVector.Y = (hl.Y - el.Y);
                    }
                    HandShapeModel handModel = null;
                    if (!isSkip)
                    {
                        //handModel = m_OpenCVController.FindHandPart(ref depthImg, out rightFront, out leftFront, headDepth - (int)CullingThresh, rightVector, leftVector,leftHandRaise);
                    }



                    // no hands detected
                    if (handModel == null)
                    {
                        handModel = new HandShapeModel(0, HandEnum.None);
                    }
                    //sw.Restart();

                    // database processing
                    DBManager db = DBManager.GetSingleton();
                    if (db != null)
                    {
                        if (skeletons != null)
                        {
                            handModel.SetSkeletonData(skeletons[0]);
                        }
                        db.AddFrameData(handModel);
                    }
                    // not recording show prob
                    else
                    {
                        //Image<Bgr, byte>[] result = HandShapeClassifier.GetSingleton()
                        //.RecognizeGesture(handModel.hogRight, 3);
                        ////Console.WriteLine(sw.ElapsedMilliseconds);
                        //if (result != null)
                        //{
                        //    ImageConverter.UpdateWriteBMP(WrtBMP_Candidate1, result[0].Convert<Gray, byte>().ToBitmap());
                        //    ImageConverter.UpdateWriteBMP(WrtBMP_Candidate2, result[1].Convert<Gray, byte>().ToBitmap());
                        //    ImageConverter.UpdateWriteBMP(WrtBMP_Candidate3, result[2].Convert<Gray, byte>().ToBitmap());
                        //}
                    }
                    string currentSign = db == null ? "0" : db.CurrentSign.ToString();
                    string path = @"J:\Kinect data\Aaron 141-180\hands\" + currentSign + " " + handModel.frame.ToString();
                    // UI update
                    if (rightFront != null)
                    {
                        Bitmap right = rightFront.ToBitmap();
                        //right.Save(path + " r.jpg");
                        ImageConverter.UpdateWriteBMP(WrtBMP_RightHandFront, right);
                    }
                    if (leftFront != null)
                    {
                        Bitmap left = leftFront.ToBitmap();
                        //left.Save(path + " l.jpg");
                        ImageConverter.UpdateWriteBMP(WrtBMP_LeftHandFront, left);
                    }
                    if (sw.ElapsedMilliseconds>15)
                    {
                        Console.WriteLine("Find hand:" + sw.ElapsedMilliseconds);
                    }
                    sw.Restart();

                    //**************************draw gray histogram
                    //Bitmap histo = m_OpenCVController.Histogram(depthImg);
                    //ImageConverter.UpdateWriteBMP(GrayWriteBitmap, histo);

                    //  draw hand position from kinect
                    // DrawHandPosition(depthBMP, rightHandPosition, System.Drawing.Brushes.Purple);

                    //*******************upadte UI
                    ImageConverter.UpdateWriteBMP(DepthWriteBitmap, depthImg.ToBitmap());
                    // Console.WriteLine("Update UI:" + sw.ElapsedMilliseconds);

                }
            }


        }

        public override void Run()
        {


        }
        public override void Stop()
        {

        }



        private System.Drawing.Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new System.Drawing.Point(depthPoint.X, depthPoint.Y);
        }


        static float KinectFOV = 0.8378f;
        float TanTiltAngle = 0;
        private float CalTiltAngle(int y, int upperDepth, int lowerPartDepth)
        {
            if (lowerPartDepth == 0)
            {
                TanTiltAngle = 0;
                return 0;
            }
            int depthDiff = upperDepth - lowerPartDepth;
            float longEdge = (float)Math.Tan(KinectFOV / 2) *
                 (headDepth * (float)(240 - y) / 240 + upperDepth - depthDiff);
            float shortEdge = depthDiff;
            TanTiltAngle = shortEdge / longEdge;
            TanTiltAngle = TanTiltAngle > 1 ? 0 : TanTiltAngle;
            double angle = Math.Atan(TanTiltAngle);
            return (float)angle;

        }





        public override void Shutdown()
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        public override void Start()
        {
            // Start the sensor!
            try
            {
                this.sensor.Start();
            }
            catch (Exception)
            {
                this.sensor = null;
            }
        }

        public override void Reset()
        {
            base.Reset();
            headDepth = 100;
            headPosition = new Point();
        }



    }
}