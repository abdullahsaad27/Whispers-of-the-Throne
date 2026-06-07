using System;
using System.Collections.Generic;
using System.Speech.Synthesis;

namespace KingdomBlind_CSharp.Audio
{
    public class SapiEngine : IDisposable
    {
        private SpeechSynthesizer synth;
        public bool IsEnabled { get; set; } = true;

        public SapiEngine()
        {
            synth = new SpeechSynthesizer();
            InitializeSapi();
        }

        private void InitializeSapi()
        {
            try
            {
                synth.Rate = 5; // Fast speed for screen readers
                foreach (var voice in synth.GetInstalledVoices())
                {
                    if (voice.VoiceInfo.Description.Contains("Leila") || voice.VoiceInfo.Name.Contains("Arabic"))
                    {
                        synth.SelectVoice(voice.VoiceInfo.Name);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SAPI initialization error: " + ex.Message);
            }
        }

        public List<string> GetAvailableVoices()
        {
            var voices = new List<string>();
            try
            {
                foreach (var voice in synth.GetInstalledVoices())
                {
                    voices.Add(voice.VoiceInfo.Name);
                }
            }
            catch {}
            return voices;
        }

        public void SetVoice(string voiceName)
        {
            if (string.IsNullOrEmpty(voiceName)) return;
            try { synth.SelectVoice(voiceName); } catch {}
        }

        public void Speak(string text, bool interrupt = true)
        {
            if (!IsEnabled) return; 

            if (interrupt)
            {
                synth.SpeakAsyncCancelAll();
            }
            synth.SpeakAsync(text);
        }

        public void Stop()
        {
            synth.SpeakAsyncCancelAll();
        }

        public void Dispose()
        {
            synth.SpeakAsyncCancelAll();
            synth.Dispose();
        }
    }
}
