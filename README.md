# SpeedDownload: Idle Megabit Tycoon

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Unity 6](https://img.shields.io/badge/Unity-6000.5.0f1-black.svg?style=flat&logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP%202D-blue.svg)](https://unity.com/srp/Universal-Render-Pipeline)
[![Tests](https://img.shields.io/badge/Unit%20Tests-31%2F31%20Passed-brightgreen.svg)]()
[![Soak Stress Test](https://img.shields.io/badge/Soak%20Test-3600%20Frames%20Pass-brightgreen.svg)]()
[![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20WebGL-green.svg)]()
[![Play in Browser](https://img.shields.io/badge/Play-in%20Browser-181717?logo=github)](https://burakyildizgamedev.github.io/SpeedDownload-Idle-Megabit-Tycoon/)
[![itch.io](https://img.shields.io/badge/Play%20on-itch.io-FA5C5C?logo=itchdotio&logoColor=white)](https://burak-yildiz.itch.io/megabit-tycoon)

> **SpeedDownload: Idle Megabit Tycoon** is a retro-modern 2D idle/clicker simulation game where players upgrade from vintage dial-up speeds (`bps`) to hyper-scale fiber networks (`Tbps` and beyond). 

---

## 🎮 Game Overview

In *SpeedDownload*, players manage an analog/digital **Speed Limiter** tachometer. Tapping boosts instantaneous download speed to complete digital file downloads, earning revenue to unlock faster connection tiers, cutting-edge hardware, and automated network infrastructure.

### Core Features
- **Tachometer-Driven Core Loop:** Interactive speed dial giving instant visual and mechanical feedback.
- **Exponential Progression:** Balanced mathematical growth taking players through historical internet eras (56k Dial-up $\rightarrow$ DSL $\rightarrow$ Cable $\rightarrow$ Fiber $\rightarrow$ Quantum Terabits).
- **Decoupled Prestige System:** Fiber credits and permanent infrastructure multiplier cards.
- **Anti-Cheat & Save Encryption:** PBKDF2/AES-GCM encrypted persistence with automated byte-tamper detection.
- **Procedural Audio Synthesis:** Dynamic, lightweight audio and chime generation with minimal memory footprint.
- **Offline Progress & Daily Quests:** Time-tested offline catch-up calculations and seeded daily challenges.

---

## 🏗️ Technical Architecture & Stack

```
Assets/Scripts/
├── Core/          # Central Managers (Game, Wallet, Economy, Save, Audio, Tier)
├── Data/          # ScriptableObject definitions (Upgrades, Connection Tiers, File Data)
├── UI/            # Decoupled MVP Views, Safe-Area Fitter, Localization & FX
├── Util/          # Zero-allocation NumberFormatter, BigNumber & math utilities
└── Editor/        # Custom Tools (Soak Tester, Sprite Chroma-Key Importer, Balance Tools)
    └── Tests/     # NUnit Unit Test Suite (31 unit & edge-case tests)
```

- **Engine:** Unity 6 (`6000.5.0f1`), Universal Render Pipeline (URP 2D).
- **Architecture:** ScriptableObject-driven architecture + Event-driven message bus (`GameEventManager`).
- **Memory & Performance:** Zero-garbage allocation in steady-state gameplay loops; optimized for mobile IL2CPP build stripping.
- **Editor Tooling:** Custom editors for bulk balance tuning, automatic sprite chroma-keying, and stress testing.

---

## 🤖 AI-Assisted Engineering Workflow

This project was built leveraging a modern **Human-in-the-Loop (HITL) AI Pair-Programming** approach with Google Gemini. 

Rather than relying on unverified generative code, AI was orchestrated under strict architectural boundaries to write high-performance math formulas, build comprehensive test suites, and generate cohesive vector art assets.

👉 **Read the full case study & prompt templates:**  
📄 **[AI_WORKFLOW.md](AI_WORKFLOW.md)** — *Architecture, Prompt Showcase, Cryptographic Security & Verification Pipeline.*

---

## 🧪 Testing & Verification

The project includes an automated test harness designed for continuous verification:

1. **Unit Testing Suite (`UnitTests.cs`):**
   - 31 unit tests covering wallet double-spending, geometric bulk buy, milestone integrity, and cryptographic tampering.
   - Run via Unity Editor Test Runner or `SpeedDownload.EditorTools.Tests.UnitTests.RunAllUnitTests()`.
2. **Automated Soak Runner (`SoakTestTool.cs`):**
   - Simulates 3,600+ frames of accelerated gameplay to guarantee zero memory leaks and zero runtime exceptions.

---

## 🚀 Getting Started

1. Clone this repository:
   ```bash
   git clone https://github.com/BurakYildizGameDev/SpeedDownload-Idle-Megabit-Tycoon.git
   ```
2. Open the project in **Unity 6 (6000.5.0f1)** or newer.
3. Open `Assets/Scenes/Game.unity` and press **Play**.
4. To run tests: Open `Window > General > Test Runner` or execute `UnitTests.RunAllUnitTests()` from the custom editor menu.

### Building for WebGL
- In the editor: **Tools > SpeedDownload > 6. Build WebGL (itch.io + GitHub Pages)**.
- Output: `Builds/WebGL/` (for GitHub Pages) and `Builds/MegabitTycoon-WebGL.zip` (upload to itch.io as an HTML game, viewport 540×960).
- The build uses Gzip with decompression fallback, so it runs on hosts that don't set `Content-Encoding` headers.
- Test locally over HTTP (opening `index.html` via `file://` does not work), e.g. `python -m http.server` inside `Builds/WebGL`.
- On WebGL the AdMob SDK is disabled; rewarded placements use the built-in in-game panel instead.

---

## 📄 License & Credits
- **Developed by:** Burak
- **AI Copilot & Prompt Design:** Google Gemini
- **License:** [MIT](LICENSE)
