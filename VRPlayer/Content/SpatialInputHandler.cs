using System;
using System.Diagnostics;
using Windows.UI.Input.Spatial;

namespace VRPlayer.Common
{
    public class SpatialInputHandler
    {
        private SpatialInteractionManager interactionManager;
        private SpatialGestureRecognizer gestureRecognizer;
        public SpatialGestureSettings GestureStatus;
        private DateTime holdStartTime;

        public TimeSpan HoldTotalTime { get; private set; }

        public SpatialInputHandler()
        {
            interactionManager = SpatialInteractionManager.GetForCurrentView();
            interactionManager.InteractionDetected += OnInteractionDetected;

            gestureRecognizer = new SpatialGestureRecognizer(
                SpatialGestureSettings.Tap |
                SpatialGestureSettings.DoubleTap |
                SpatialGestureSettings.Hold
                );
            gestureRecognizer.Tapped += OnTap;
            gestureRecognizer.HoldStarted += OnHoldStarted;
            gestureRecognizer.HoldCompleted += OnHoldComleted;
            
        }

        /// <summary>
        /// Capture Spatial Gesture
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnInteractionDetected(SpatialInteractionManager sender, SpatialInteractionDetectedEventArgs args)
        {
            gestureRecognizer.CaptureInteraction(args.Interaction);
        }

        /// <summary>
        /// OnHoldStarted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnHoldStarted(SpatialGestureRecognizer sender, SpatialHoldStartedEventArgs args)
        {
            holdStartTime = DateTime.Now;
        }

        private void OnHoldComleted(SpatialGestureRecognizer sender, SpatialHoldCompletedEventArgs args)
        {
            GestureStatus = SpatialGestureSettings.Hold;
            HoldTotalTime = DateTime.Now - holdStartTime;
        }


        public SpatialGestureSettings CheckGestureStatus()
        {
            var gestureStatus = this.GestureStatus;
            this.GestureStatus = SpatialGestureSettings.None;
            return gestureStatus;
        }

        private void OnTap(SpatialGestureRecognizer sender, SpatialTappedEventArgs args)
        {
            GestureStatus = (args.TapCount == 2) ? 
                SpatialGestureSettings.DoubleTap : SpatialGestureSettings.Tap;
        }
    }
}