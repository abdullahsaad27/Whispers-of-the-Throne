using System;
using KingdomBlind_CSharp.Audio;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public interface IAudioService : IDisposable
    {
        void Play(string category, bool async = true, bool forceNoLoop = false);
        void PlayTick();
        void PlayPaper();
        void PlaySuccess();
        void PlayError();
        void StopAmbient();
        void StopAll();
        void SetDucking(bool enabled);
        void SetPan(string category, float pan);
    }

    public interface INarrationService
    {
        void Speak(GameState state, string text, bool isNpcDialog = false, bool interrupt = true);
        void Stop();
    }

    public sealed class NarrationService : INarrationService
    {
        private readonly SapiEngine sapi;

        public NarrationService(SapiEngine sapi)
        {
            this.sapi = sapi;
        }

        public void Speak(GameState state, string text, bool isNpcDialog = false, bool interrupt = true)
        {
            if (state == null || string.IsNullOrWhiteSpace(text))
                return;

            if (state.SpeechProvider == "sapi5")
            {
                if (isNpcDialog && !state.SapiReadsNPCs) return;
                if (!isNpcDialog && !state.SapiReadsEvents) return;
                sapi.Speak(text, interrupt);
            }
            else if (state.SpeechProvider == "nvda" && NvdaEngine.IsRunning())
            {
                NvdaEngine.Speak(text);
            }
        }

        public void Stop()
        {
            sapi.Stop();
        }
    }
}
