using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WhispersOfTheThrone.Systems
{
    public class AudioManager : IAudioService
    {
        [DllImport("winmm.dll")]
        private static extern long mciSendString(string command, StringBuilder returnString, int returnLength, IntPtr hwndCallback);

        private readonly string assetsDir;
        private readonly Dictionary<string, string> audioMap;
        private readonly Dictionary<string, float> panByCategory = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private bool duckingEnabled;

        public AudioManager()
        {
            assetsDir = Path.Combine(AppContext.BaseDirectory, "assets");
            if (!Directory.Exists(assetsDir))
            {
                assetsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets");
            }

            audioMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "tick", "ui_click.mp3" },
                { "success", "Interface Plucks Happy.mp3" },
                { "error", "Interface Deny Low Fat Dark.mp3" },
                { "paper", "ui_paper.mp3" },
                { "bag_open", "CLOTHFlp_Action Inventory Open Flip Cloth Canvas Bag Slide Light 02_ESM_FG2.mp3" },
                { "coin", "Ting Coins.mp3" },
                { "coins", "Foley Coin Flip Single Fast.mp3" },
                { "door_heavy", "METLImpt_Metal Bangs, Metal Hits, Banging On Doors_344 Audio_Haunting Ambiences Vol 3.mp3" },
                { "build", "WOODImpt_Wooden Hit, Dark, Heavy Hit, Vampire's Prison_344 Audio_Haunting Ambiences Vol 3.mp3" },
                { "ambient_nature", "" },
                { "ambient_tavern", "amb_tavern_rich.mp3" },
                { "ambient_council", "amb_night_rich.mp3" },
                { "ambient_main_menu", "amb_main_menu.mp3" },
                { "ambient_storm", "STORM_StormAmbience11_InMotionAudio_BackGardenStorm.mp3" },
                { "ambient_dungeon", "WINDInt_ChimneyWind05_InMotionAudio_ChimneyWind.mp3" },
                { "ambient_magic", "amb_evil.mp3" },
                { "sword", "WEAPSwrd_Sword Slide Cuts, Metallic, Impact CM4 2_344 Audio_Medieval Weapons Vol 2.mp3" },
                { "sword_hit", "SWSH_SWING IMPACTS Quick Heavy Weapon Swing To Thud Impact Var 01_DDUMAIS_MWP2.mp3" },
                { "armor", "WEAPArmr_Metal Shield Spin On Floor, Buckler MKH_344 Audio_Medieval Weapons Vol 2.mp3" },
                { "army_march", "DSGNStngr_Action Deploy Units Sword Slice Special Move Layered Swish 04_ESM_TDG.mp3" },
                { "horn", "DSGNBram____Cinematic Horn Braam, Epic, Cinematic, Dark, Instrument, Huge-32.mp3" },
                { "arrow", "Arrow Hit Rattle.mp3" },
                { "magic_cast", "GAMEMisc_Magic Creation 23_CB Sounddesign_APPlicable Sounds.mp3" },
                { "magic_dark", "magic, action gesture, evil presence, onslaught-004.mp3" },
                { "bell", "04 Church Bells, Near Distance, In Church Tower-3 Different Bell 02.mp3" },
                { "cry", "VOXCry_Crying Female 04 05_SNDBTS_VH.mp3" },
                { "harp", "ui_hover.mp3" },
                { "beacon", "02 Fireworks_ explosions_dense_whistles.mp3" },
                { "wall", "WOODImpt_Wooden Hit, Dark, Heavy Hit, Vampire's Prison_344 Audio_Haunting Ambiences Vol 3.mp3" },
                { "ui_click_01", "ui_click_01.mp3" },
                { "ui_click_02", "ui_click_02.mp3" },
                { "ui_hover_01", "ui_hover_01.mp3" },
                { "ui_select", "ui_select.mp3" },
                { "ambient_court", "amb_court_rich.mp3" },
                { "ambient_war_camp", "amb_war_camp.mp3" },
                { "battle_swords", "battle_swords.mp3" },
                { "battle_horses", "battle_horses.mp3" },
                { "battle_charge", "battle_charge.mp3" },
                { "battle_victory", "battle_victory.mp3" },
                { "battle_defeat", "battle_defeat.mp3" },
                { "intrigue_whisper", "intrigue_whisper.mp3" },
                { "scheme_discovered", "scheme_discovered.mp3" },
                { "council_debate", "council_debate.mp3" },
                { "genetics_birth", "genetics_birth.mp3" },
                { "terrain_march", "terrain_march.mp3" },
                { "paper_scroll", "paper_scroll.mp3" },
                { "door_open", "door_open.mp3" },
                { "door_close", "door_close.mp3" },
                { "siege_weapon", "siege_weapon.mp3" },
                { "army_march_heavy", "army_march_heavy.mp3" },
                { "arrow_volley", "arrow_volley.mp3" },
                { "amb_court", "amb_court.mp3" },
                { "amb_war_camp", "amb_war_camp.mp3" },
                { "amb_market", "amb_market.mp3" },
                { "amb_desert", "amb_desert.mp3" },
                { "amb_rain", "amb_rain.mp3" },
                { "amb_winter", "amb_winter.mp3" },
                { "amb_spring", "amb_spring.mp3" },
                { "season_harvest", "season_harvest.mp3" },
                { "prison_door", "prison_door.mp3" },
                { "prison_chains", "prison_chains.mp3" },
                { "dungeon_ambient", "dungeon_ambient.mp3" },
                { "execution_drums", "execution_drums.mp3" },
                { "poetry_recital", "poetry_recital.mp3" },
                { "religious_chant", "religious_chant.mp3" },
                { "horse_hooves", "horse_hooves.mp3" },
                { "coin_pouch", "coin_pouch.mp3" },
                { "letter_seal", "letter_seal.mp3" },
                { "poison_vial", "poison_vial.mp3" }
            };
        }

        public void Play(string category, bool async = true, bool forceNoLoop = false)
        {
            try
            {
                // To support stripping ".mp3" from old calls
                if (category.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    category = category.Substring(0, category.Length - 4);
                }

                if (audioMap.TryGetValue(category, out string fileName))
                {
                    if (string.IsNullOrEmpty(fileName)) return;
                    string filePath = Path.GetFullPath(Path.Combine(assetsDir, fileName));
                    if (File.Exists(filePath))
                    {
                        string alias = category; 
                        mciSendString($"close {alias}", null, 0, IntPtr.Zero);
                        mciSendString($"open \"{filePath}\" type mpegvideo alias {alias}", null, 0, IntPtr.Zero);
                        
                        string playCmd = $"play {alias}";
                        bool shouldLoop = (category.StartsWith("ambient", StringComparison.OrdinalIgnoreCase) && !forceNoLoop);
                        if (shouldLoop) playCmd += " repeat";
                        mciSendString(playCmd, null, 0, IntPtr.Zero);

                        // winmm has no reliable panning/ducking API for wave aliases.
                        // The values are kept so a later NAudio/FMOD backend can honor them.
                        _ = duckingEnabled;
                        _ = panByCategory;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing sound {category}: {ex.Message}");
            }
        }

        public void PlayTick()
        {
            Play("tick");
        }

        public void PlayPaper()
        {
            Play("paper");
        }
        
        public void PlaySuccess()
        {
            Play("success");
        }
        
        public void PlayError()
        {
            Play("error");
        }

        public void StopAll()
        {
            try
            {
                foreach(var key in audioMap.Keys)
                {
                    mciSendString($"close {key}", null, 0, IntPtr.Zero);
                }
            }
            catch { }
        }
        
        // Stop a specific background ambient
        public void StopAmbient()
        {
            mciSendString("close ambient_nature", null, 0, IntPtr.Zero);
            mciSendString("close ambient_tavern", null, 0, IntPtr.Zero);
            mciSendString("close ambient_council", null, 0, IntPtr.Zero);
            mciSendString("close ambient_storm", null, 0, IntPtr.Zero);
            mciSendString("close ambient_dungeon", null, 0, IntPtr.Zero);
            mciSendString("close ambient_magic", null, 0, IntPtr.Zero);
        }

        public void SetDucking(bool enabled)
        {
            duckingEnabled = enabled;
        }

        public void SetPan(string category, float pan)
        {
            if (string.IsNullOrWhiteSpace(category))
                return;

            panByCategory[category] = Math.Max(-1f, Math.Min(1f, pan));
        }

        public void Dispose()
        {
            StopAll();
        }
    }
}
