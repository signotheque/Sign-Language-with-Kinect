﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;

using Microsoft.Kinect;
using System.IO;

using Emgu.CV;
using Emgu.Util;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;
using System.Data.SQLite;
using Emgu.CV.Structure;
namespace CURELab.SignLanguage.HandDetector
{

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary
        public WriteableBitmap colorBitmap;


        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        public WriteableBitmap depthBitmap;

        public WriteableBitmap grayBitmap;
        private KinectController m_KinectController;
        private OpenCVController m_OpenCVController;
        private KinectStudioController m_kinectStudioController;
        private DBManager m_DBmanager;

        public MainWindow()
        {
            InitializeComponent();
            m_kinectStudioController = KinectStudioController.GetSingleton();
        }



        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            m_OpenCVController = OpenCVController.GetSingletonInstance();

            RegisterThreshold("canny", ref OpenCVController.CANNY_THRESH, 100, 8);
            RegisterThreshold("cannyThresh", ref OpenCVController.CANNY_CONNECT_THRESH, 100, 22);
            RegisterThreshold("play speed", ref OpenNIController.SPEED, 2, 1);
            RegisterThreshold("diff", ref KinectController.DIFF, 10, 7);
            RegisterThreshold("Culling", ref KinectSDKController.CullingThresh, 100, 30);


            //  Menu_ONI_Click(this, e);
            Menu_Kinect_Click(this, e);
            //MenuItem_Test_Click(this, e);
        }

        private unsafe void RegisterThreshold(string valuename, ref double thresh, double max, double initialValue)
        {

            fixed (double* ptr = &thresh)
            {
                thresh = initialValue;
                TrackBar tcb = new TrackBar(ptr);
                tcb.Max = max;
                tcb.Margin = new Thickness(5);
                tcb.ValueName = valuename;
                initialValue = initialValue > max ? max : initialValue;
                tcb.Value = initialValue;
                SPn_right.Children.Add(tcb);
            }

        }



        private void Initialize()
        {
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (m_KinectController != null)
            {
                m_KinectController.Shutdown();

            }
        }


        private void Kinect_Setup()
        {

        }

        private void Menu_Exit_Click(object sender, RoutedEventArgs e)
        {

            if (m_KinectController != null)
            {
                m_KinectController.Shutdown();

            }
            Environment.Exit(0);
        }

        private void Menu_OpenNI_Click(object sender, RoutedEventArgs e)
        {
            ResetAll();
            m_KinectController = OpenNIController.GetSingletonInstance();
            statusBar.DataContext = m_KinectController;
            m_KinectController.Initialize();
            this.img_color.Source = m_KinectController.ColorWriteBitmap;
            this.img_depth.Source = m_KinectController.ProcessedDepthBitmap;
            m_KinectController.Start();
        }

        private void Menu_ONI_Click(object sender, RoutedEventArgs e)
        {
            ResetAll();
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.DefaultExt = ".oni";
            ofd.Filter = "oni file|*.oni";
            if (ofd.ShowDialog() == true)
            {
                m_KinectController = OpenNIController.GetSingletonInstance();
                statusBar.DataContext = m_KinectController;
                m_KinectController.Initialize(ofd.FileName);
                this.img_color.Source = m_KinectController.ColorWriteBitmap;
                this.img_depth.Source = m_KinectController.ProcessedDepthBitmap;
                this.img_leftFront.Source = m_KinectController.GrayWriteBitmap;
                m_KinectController.Start();
            }

        }

        private void Menu_Kinect_Click(object sender, RoutedEventArgs e)
        {
            ResetAll();
            m_KinectController = KinectSDKController.GetSingletonInstance();
            statusBar.DataContext = m_KinectController;
            m_KinectController.Initialize();
            this.img_color.Source = m_KinectController.ColorWriteBitmap;
            this.img_depth.Source = m_KinectController.DepthWriteBitmap;
            this.img_leftFront.Source = m_KinectController.WrtBMP_LeftHandFront;
            this.img_rightFront.Source = m_KinectController.WrtBMP_RightHandFront;
            this.img_rightSide.Source = m_KinectController.BI_RightHandRecognized;
            this.img_leftSide.Source = m_KinectController.BI_LeftHandRecognized;

            m_KinectController.Start();
        }


        private void Menu_Gesture_Click(object sender, RoutedEventArgs e)
        {
            ResetAll();
            m_KinectController = KinectSDKController.GetSingletonInstance();
            statusBar.DataContext = m_KinectController;
            m_KinectController.Initialize();
            this.img_color.Source = m_KinectController.ColorWriteBitmap;
            this.img_depth.Source = m_KinectController.DepthWriteBitmap;
            this.img_leftFront.Source = m_KinectController.WrtBMP_LeftHandFront;
            this.img_rightFront.Source = m_KinectController.WrtBMP_RightHandFront;
            this.img_rightSide.Source = m_KinectController.BI_RightHandRecognized;
            this.img_leftSide.Source = m_KinectController.BI_LeftHandRecognized;
            m_KinectController.Start();
        }

        private void ResetAll()
        {
            if (m_KinectController != null)
            {
                m_KinectController.Shutdown();
                m_KinectController.Reset();
                m_KinectController = null;
                GC.Collect();
            }
        }

        private void Window_KeyDown_1(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {

                case Key.Space:
                    m_KinectController.TogglePause();
                    break;
                default:
                    break;
            }
        }


        private bool _isConnected;

        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
            set
            {
                _isConnected = value;
                if (_isConnected)
                {
                    statusBarKinectStudio.Text = "Kinect Studio Connected";
                }
                else
                {
                    statusBarKinectStudio.Text = "Kinect Studio Not Connected";
                }
            }
        }
        private void MenuItem_Connect_Click(object sender, RoutedEventArgs e)
        {
            IsConnected = m_kinectStudioController.Connect();
        }

        private void MenuItem_Start_Click(object sender, RoutedEventArgs e)
        {
            IsConnected = m_kinectStudioController.Start();
        }

        private List<SignWordModel> wordList;
        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string folderName = dialog.SelectedPath;
                string dbPath = folderName + "\\data.db";
                m_DBmanager = DBManager.GetSingleton(dbPath);
                DirectoryInfo folder = new DirectoryInfo(folderName);
                wordList = new List<SignWordModel>();
                foreach (var item in folder.GetFiles("*.xed"))
                {
                    string fileName = item.Name;
                    string[] s = fileName.Split();
                    SignWordModel wordModel = new SignWordModel(s[0],s[1],item.FullName, fileName);
                    wordList.Add(wordModel);
                } 
            }
           
        }

        int signIndex = 0;
        private void MenuItem_Run_Click(object sender, RoutedEventArgs e)
        {
            signIndex = 0;
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += timer_Tick;
            timer.Start();

        }
        private void MenuItem_Test_Click(object sender, RoutedEventArgs e)
        {
            HandShapeClassifier hsc = HandShapeClassifier.GetSingleton();
            string path = @"C:\Users\Administrator\Desktop\handshapes\";
            
            for (int i = 0; i < 5; i++)
            {
                Image<Bgr, byte> image = new Image<Bgr, byte>(path + "handshape" + (i/4+1) + "-"+ (i%4+1)+".jpg");
                this.img_rightSide.Source = hsc.RecognizeGesture(image).ToBitmap().ToBitmapSource();
            }
            //m_DBmanager.Test();

        }

       
        void timer_Tick(object sender, EventArgs e)
        {
            //end
            if (signIndex >= wordList.Count())
            {
                Console.WriteLine("finish");
                ((System.Windows.Forms.Timer)sender).Stop();
            }
            if (!m_DBmanager.Begin)
            {
                //begin
                Console.WriteLine("[{0}/{1} {2:P}] \nloading:{3}",
                    signIndex,wordList.Count(),
                    (float)signIndex/wordList.Count(),
                    wordList[signIndex].File);
                m_DBmanager.AddWordModel(wordList[signIndex]);
                m_kinectStudioController.Open_File(wordList[signIndex].FullName);
                m_kinectStudioController.Run();
                m_DBmanager.Begin = true;
                signIndex++;
            }
            else
            {
                //running
                Console.WriteLine("waiting");
            }
        }

 


    }
}
