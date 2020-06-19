﻿using ImageProcessor;
using ImageProcessor.Imaging.Filters.Photo;
using LedDashboardCore;
using LedDashboardCore.Modules.BasicAnimation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Games.RocketLeague
{
    class GoalModule : LEDModule
    {
        // Constants

        public static HSVColor BurstColor { get; } = new HSVColor(0.12f, 0.1f, 1f);

        private static int GOAL_COOLDOWN_MS = 5000;

        private static string GOAL_ANIM_PATH = "Animations/RocketLeague/goal.txt";

        // Members
        public bool IsPlayingAnimation => goalOnCooldown;

        // Events

        public event LEDModule.FrameReadyHandler NewFrameReady;

        // Variables

        Bitmap lastCapture;
        AnimationModule animator;
        bool goalOnCooldown;

        public GoalModule()
        {
            animator = AnimationModule.Create();
            animator.PreloadAnimation(GOAL_ANIM_PATH);
            animator.NewFrameReady += NewFrameReadyHandler;
        }

        private void NewFrameReadyHandler(LEDFrame frame)
        {
            frame.SenderChain.Add(this);
            NewFrameReady?.Invoke(frame);
        }

        /// <summary>
        /// Gets the LEDFrame for the boost view
        /// </summary>
        public void DoFrame(Bitmap screenFrame)
        {
            if (goalOnCooldown)
                return;
            // Set the cropping region
            // It might change depending on the resolution. Right now it works for 1920x1080
            // ROCKET LEAGUE @ 1920x1080: left 290, top 290, width 220, height 240
            // ROCKET LEAGUE GOAL LEFT @ 1920x1080: 1920/2 - 202, 10, 95, 85
           // Rectangle cropRect = new Rectangle(1920 / 2 + 102, 10, 70, 70); // --- right goal indicator
            Rectangle cropRect = new Rectangle(1920 / 2 - 172, 15, 70, 70); // ---left goal indicator

            // TODO: After goal, maybe capture the timer to figure out when kick off has started
            // TODO: Or maybe look at the lower part of the screen to check for black stripes.

            // Get the screen frame
            if (screenFrame != null)
            {
                // Resize the bitmap
                Bitmap target = new Bitmap(64, 64);

                // int left = cropRect.X;
                //int top = cropRect.Y;
                using (Graphics g = Graphics.FromImage(target))
                {
                    // int diffWidth = frameBitmap.Width - left;
                    // int diffheight = frameBitmap.Height - top;
                    g.DrawImage(screenFrame, new Rectangle(0, 0, target.Width, target.Height),
                                     cropRect,
                                     GraphicsUnit.Pixel);
                }

                ProcessFrame(target);
            }
            
        }

        private static List<int> alreadyTouchedLeds = new List<int>(); // fixes a weird flickering bug

        

        private void ProcessFrame(Bitmap b)
        {
            using (ImageFactory imageFactory = new ImageFactory(preserveExifData: true))
            {
                try
                {
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        // Load, resize, set the format and quality and save an image.
                        imageFactory
                            .Load(b)
                            .Filter(MatrixFilters.GreyScale)
                            .Contrast(80)
                            .Save(memStream);

                        b.Dispose();

                        Bitmap bpls = (Bitmap)Bitmap.FromStream(memStream);

                        for (int i = 0; i < bpls.Width; i++)
                        {
                            for (int j = 0; j < bpls.Height; j++)
                            {
                                Color c = bpls.GetPixel(i, j);
                                if (c.R < 252 || c.G < 252 || c.B < 252)
                                {
                                    bpls.SetPixel(i, j, Color.Black);
                                }
                                else
                                {
                                    bpls.SetPixel(i, j, Color.White);
                                }
                            }
                        }

                        //bpls.Save(fileName);
                        int differentPixels = 0;
                        int DIFFERENT_THRESHOLD_MIN = 20;
                        int DIFFERENT_THRESHOLD_MAX = 50;
                        try
                        {

                            if (lastCapture != null && bpls.GetHashCode() != lastCapture.GetHashCode())
                            {
                                for (int i = 0; i < bpls.Width; i++)
                                {
                                    for (int j = 0; j < bpls.Height; j++)
                                    {
                                        Color c1 = lastCapture.GetPixel(i, j);
                                        Color c2 = bpls.GetPixel(i, j);
                                        if (c1.R != c2.R || c1.G != c2.G || c1.B != c2.B)
                                        {
                                            differentPixels++;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {

                        }

                        if (differentPixels > DIFFERENT_THRESHOLD_MIN && differentPixels < DIFFERENT_THRESHOLD_MAX)
                        {
                            if (!goalOnCooldown)
                                GoalDetected();
                        }

                        lastCapture?.Dispose();
                        lastCapture = bpls;
                    }

                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    Debug.WriteLine("Couldnt save image");
                }

            }
        }

        /// <summary>
        /// Called when a change in the scoreboard is detected.
        /// </summary>
        private void GoalDetected()
        {
            Debug.WriteLine("GOAL!");
            goalOnCooldown = true;

            // Play animation
            animator.ColorBurst(BurstColor, LightZone.All, 1f);
            animator.RunAnimationOnce(GOAL_ANIM_PATH, LightZone.All, 0, false, timeScale: 0.8f);
            animator.ColorBurst(BurstColor, LightZone.All, 2f, false);

            _ = Task.Run(async () =>
            {
                await Task.Delay(GOAL_COOLDOWN_MS);
                goalOnCooldown = false;
            });
        }        

        public void Dispose()
        {

        }
    }
}
