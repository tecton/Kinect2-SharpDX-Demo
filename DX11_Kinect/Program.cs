/* 
 * Ning Tang
 * 2015/1/20
 */
using System;
using SharpDX;

namespace DX11_Kinect
{
    /// <summary>
    /// SharpDX MiniCubeTexture Direct3D 11 Sample
    /// </summary>
    internal static class Program
    {
        static Monitor monitor = new Monitor();

        [STAThread]
        private static void Main()
        {
            monitor.init();

            var kinectHelper = new KinectHelper();
            monitor.setDepthMapSize(kinectHelper.DepthFrameDescription.Width, kinectHelper.DepthFrameDescription.Height);

            // Prepare matrices
            var view = Matrix.LookAtLH(new Vector3(0, 0, -4), new Vector3(0, 0, 0), Vector3.UnitY);
            var proj = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, monitor.form.ClientSize.Width / (float)monitor.form.ClientSize.Height, 0.1f, 100.0f);
            var viewProj = Matrix.Multiply(view, proj);
            monitor.setDepthMapMatrix(viewProj);
            monitor.render(() =>
                {
                    monitor.renewDepthmap(kinectHelper.depthPixels);
                });
        }
    }
}