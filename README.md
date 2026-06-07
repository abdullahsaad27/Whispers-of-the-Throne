# Whispers of the Throne (همسات العرش) 👑

**Whispers of the Throne** is a blind-accessible grand strategy RPG inspired by the deep philosophy and mechanics of *Crusader Kings*. It allows players to lead their dynasty, manage provinces, command armies, and navigate court intrigues within a fully immersive audio and text environment designed natively for screen readers.

## 🌟 Key Features

*   **100% Blind-Accessible:** Natively integrated with NVDA and SAPI screen readers. The UI is designed to prevent "tab fatigue" and cognitive overload by using smart dynamic categories and accessible ARIA-equivalent labels.
*   **Dynasty & Court Management:** Interact with your royal family, council members, and governors. Marry, manage relationships, and balance the loyalty of your subjects.
*   **Grand Strategy & Economics:** Oversee provinces, manage the treasury, collect taxes, and construct infrastructure to strengthen your kingdom.
*   **Military & Diplomacy:** Command armies, declare wars, manage neighbor relations, and use espionage (Intel) to uncover plots and secrets.
*   **Immersive Soundscapes:** Features over 50 high-quality, dynamic audio files ranging from epic horn blasts and sword clashes to 20-minute immersive background ambiences (storms, taverns, royal courts, and magic). The game uses a custom Audio Manager built on Windows MCI (`mpegvideo`) for lightweight `.mp3` playback.
*   **Dynamic Event System:** Face unpredictable random events, natural disasters, and political crises that test your leadership every turn.

## 🛠️ Technical Details

*   **Engine:** Built entirely in C# using Windows Forms (.NET).
*   **Audio Optimization:** All massive `.wav` assets have been compressed to `.mp3` format without losing quality, reducing the game's footprint from ~5.6 GB down to ~55 MB, making it extremely lightweight.
*   **Accessibility Tech:** Utilizes `nvdaControllerClient32.dll` for direct NVDA communication, ensuring instant, interruption-free speech feedback for the visually impaired.

## 🚀 How to Play

1. Clone or download the repository.
2. Open the solution in Visual Studio.
3. Build and run the project. The game will automatically detect if you have a screen reader active.
4. Use `Tab`, `Arrow Keys`, and standard keyboard navigation to manage your kingdom!

---
*Created as a passionate endeavor to bring the depth of Grand Strategy games to the blind and visually impaired gaming community.*
