using System;
using System.Runtime.InteropServices;

namespace KingdomBlind_CSharp.Audio
{
    public static class NvdaEngine
    {
        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern int nvdaController_speakText(string text);

        [DllImport("nvdaControllerClient32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern int nvdaController32_testIfRunning();

        [DllImport("nvdaControllerClient32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern int nvdaController32_speakText(string text);

        public static bool IsRunning()
        {
            try
            {
                if (Environment.Is64BitProcess) return nvdaController_testIfRunning() == 0;
                else return nvdaController32_testIfRunning() == 0;
            }
            catch { return false; }
        }

        public static void Speak(string text)
        {
            try
            {
                if (Environment.Is64BitProcess) nvdaController_speakText(text);
                else nvdaController32_speakText(text);
            }
            catch { /* Ignore if DLL not found */ }
        }
    }
}
