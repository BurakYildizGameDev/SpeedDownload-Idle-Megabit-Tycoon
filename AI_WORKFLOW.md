# AI-Assisted Game Engineering & Workflow Case Study
## Project: SpeedDownload — Idle Megabit Tycoon
**Platform:** Unity 6 (6000.5.0f1) · URP 2D · C# · IL2CPP / Android  
**Target Stores:** Google Play Store / itch.io  
**Author / Lead Developer:** Burak  
**AI Collaborator:** Google Gemini (High Reasoning Model)  

---

## 1. Executive Summary

In modern game development, Generative AI is often misunderstood either as a "magic button" that replaces engineering or as an unmaintainable code generator producing brittle solutions. 

This repository and document demonstrate a different, industry-ready paradigm: **Human-in-the-Loop AI Orchestration**. 

Throughout the development of *SpeedDownload: Idle Megabit Tycoon*, AI (Gemini) was utilized as a **Specialized Technical Pair Programmer & Pipeline Multiplier**. The developer defined the overarching architecture, set strict engineering constraints, performed thorough code reviews, and built automated test harnesses. Gemini was leveraged for rapid prototyping, mathematical modeling, edge-case analysis, defensive programming implementations, and rigorous 2D vector asset prompt engineering.

The result is a production-grade, commercially published mobile idle tycoon game featuring:
- A decoupled, event-driven architecture using **ScriptableObjects** and modular managers.
- **Zero-allocation / GC-friendly** runtime formatting and loops.
- Industrial-grade **AES/PBKDF2 encrypted save systems** with anti-tamper validation.
- A comprehensive **Unit & Soak Testing Suite** (31+ unit tests passing at 100%, 3600-frame automated soak test).
- A unified retro-modern visual style with over **80+ custom vector sprites** generated through a standardized prompt constraint system.

---

## 2. Core Philosophy: Human-in-the-Loop (HITL)

```
┌────────────────────────────────────────────────────────┐
│                   HUMAN DEVELOPER                      │
│  - Architecture & Design Patterns (SOLID, SO-Driven)   │
│  - Engine & Platform Knowledge (Unity 6, Android, URP) │
│  - Game Balance & Feel (UX, Progression, Feedback)     │
│  - Strict Code Review & Verification                   │
└──────────────────────────┬─────────────────────────────┘
                           │ Task Delegation & Constraints
                           ▼
┌────────────────────────────────────────────────────────┐
│                   GEMINI (AI COPILOT)                  │
│  - Boilerplate & Scaffold Generation                   │
│  - Complex Math & Geometric Progression Algorithms     │
│  - Defensive Security & Edge-Case Identification       │
│  - Comprehensive Unit Test Writing                     │
│  - Strict Parameterized Asset Prompts                  │
└──────────────────────────┬─────────────────────────────┘
                           │ Proposed Implementations
                           ▼
┌────────────────────────────────────────────────────────┐
│               VERIFICATION & QUALITY GATES             │
│  - Compiler / Static Analysis (IL2CPP compatibility)   │
│  - Automated Test Suite (`UnitTests.cs`)                │
│  - Automated Soak Runner (`SoakTestTool.cs`)           │
│  - Unity Profiler (GC Allocations, Memory, Draw Calls) │
└────────────────────────────────────────────────────────┘
```

The guiding principle was: **"AI proposes, Human reviews, Tests verify."** At no point was unverified AI code merged without structural review, memory inspection, and unit test validation.

---

## 3. Engineering Pillars & Case Studies

### 3.1. Scalable Idle Economy & BigNumber Math
* **Challenge:** Idle tycoon games suffer from exponential number overflows (from bits-per-second `bps` to petabits `Pbps` and beyond). String formatting and float precision loss in `Update()` loops lead to severe garbage collection spikes and rounding bugs.
* **AI Collaboration:**
  - Designed an optimized geometric progression cost calculator:  
    $$\text{Cost}(n) = \text{BaseCost} \times \text{Multiplier}^n$$
  - Solved closed-form bulk-buy formulas using geometric series summation:  
    $$S_k = \text{Cost}(n) \times \frac{r^k - 1}{r - 1}$$  
    enabling $O(1)$ calculations for "Buy 10x", "Buy 100x", and "Buy Max" operations without iterative loops.
  - Implemented a zero-allocation, reusable `StringBuilder` based `NumberFormatter` ([NumberFormatter.cs](file:///C:/Users/Burak/Desktop/Application/SpeedDownload%20Idle%20Megabit%20Tycoon/Assets/Scripts/Util/NumberFormatter.cs)) tested against hostile inputs and extreme values.

### 3.2. Cryptographic Save System & Anti-Cheat
* **Challenge:** Mobile games are easily decompiled, and plaintext JSON save files are routinely tampered with (arbitrary currency injection, milestone spoofing).
* **AI Collaboration:**
  - Implemented `SaveCrypto.cs` featuring:
    - **PBKDF2** key derivation with salted device-specific hardware binding.
    - **AES-GCM / HMAC** payload validation to instantly detect file truncation or byte alterations.
    - Defensive sanitization layer (`SaveData.Sanitize()`) that automatically clamps corrupted or modified save variables before deserialization.
  - Wrote explicit verification tests ([UnitTests.cs](file:///C:/Users/Burak/Desktop/Application/SpeedDownload%20Idle%20Megabit%20Tycoon/Assets/Scripts/Editor/Tests/UnitTests.cs#L37-L43)) guaranteeing that corrupted or tampered payloads fail safely with graceful fallback defaults.

### 3.3. Test-Driven Defensive Engineering
* **Challenge:** Long idle sessions cause subtle memory leaks, race conditions in ad providers, and off-by-one errors in milestone calculations.
* **AI Collaboration:**
  - Wrote **31 automated unit tests** in `Assets/Scripts/Editor/Tests/UnitTests.cs` validating:
    - Wallet double-spend and refund integrity (preventing lifetime earnings inflation exploits).
    - Offline earnings calculations and milestone consistency.
    - AdService priority resolution and stub ad cancellation callbacks.
  - Built an automated **Soak Test Tool** (`SoakTestTool.cs`) that runs simulated game loops over 3,600+ consecutive frames at 10x-50x speed to verify zero memory leaks and zero runtime exceptions.

---

## 4. Prompt Engineering Showcase

To achieve reliable, production-ready results from LLMs, prompts were structured with explicit **system constraints**, **architectural patterns**, and **failure criteria**.

### Recipe 1: High-Performance Unity C# Implementation Prompt
```markdown
[ROLE & CONTEXT]
You are a Principal Unity Engine Engineer. Target platform: Unity 6 URP 2D on Android (IL2CPP).

[TASK]
Implement the bulk-buy calculation logic for `EconomyManager.cs`.

[ARCHITECTURAL CONSTRAINTS]
1. Use closed-form geometric series summation: S_k = A * (r^k - 1) / (r - 1). Do NOT use loops for 'Buy Max'.
2. Zero GC allocation during runtime execution: do NOT allocate temporary arrays, closures, or Linq queries.
3. Handle floating point edge-cases where ratio r == 1.0 (linear fallback).
4. Provide double-precision bounds check against `double.MaxValue` to avoid overflow to Infinity.

[OUTPUT REQUIREMENTS]
- Clean C# code matching Unity C# Style Guide.
- XML docstrings explaining time and space complexity.
- Unit test method verifying exact budget boundary conditions.
```

---

### Recipe 2: Defensive Save Data Tamper-Proofing Prompt
```markdown
[OBJECTIVE]
Design a tamper-evident, encrypted serialization pipeline for `SaveData.cs`.

[SECURITY REQUIREMENTS]
- Algorithm: AES-256 with PBKDF2 (10,000 iterations) salt derivation.
- Add HMAC-SHA256 signature header to detect manual byte edits.
- Implement a `Sanitize()` pass: if any currency or upgrade level exceeds mathematical thresholds
  or is NaN/Negative, safely clamp it without crashing.
- Backwards compatibility: ensure v1 unencrypted saves cleanly migrate to v2 encrypted format.

[DELIVERABLES]
1. `SaveCrypto.cs` helper class.
2. Unit tests demonstrating:
   a) Round-trip serialization/deserialization.
   b) Tampered payload rejection.
   c) Truncated payload rejection.
```

---

### Recipe 3: Cohesive Visual Asset Generation Pipeline (Vision / Art)
*From [Docs/ASSET_PROMPTS.md](file:///C:/Users/Burak/Desktop/Application/SpeedDownload%20Idle%20Megabit%20Tycoon/Docs/ASSET_PROMPTS.md)*

One of the largest pitfalls in AI-generated game art is **stylistic inconsistency** (some sprites look photorealistic, others cartoonish, others 3D). To solve this, a 2-part prompt structure was enforced:

```
┌─────────────────────────────────┐      ┌─────────────────────────┐
│       FIXED STYLE BLOCK         │  +   │    VARIABLE SUBJECT     │
│ (Palette, stroke, lighting,     │      │ (Specific icon details, │
│  chroma-key #FF00FF background) │      │  focal point, accents)  │
└─────────────────────────────────┘      └─────────────────────────┘
```

**Standardized Master Style Block:**
```text
Flat vector icon illustration, retro-modern UI style, 1:1 square canvas.

STYLE RULES (strict):
- Bold uniform outline, deep navy color #273548, consistent stroke weight
  (roughly 12px at 512x512 resolution) on every edge of every shape.
- Flat solid fills ONLY. Absolutely no gradients, no drop shadows, no glow,
  no ambient occlusion, no 3D rendering, no bevel, no texture, no noise.
- Limited palette: neutral gray #A8ACB8 as main body, off-white #FCFCFC for light surfaces,
  blue #4A90E2 used sparingly as a single accent on the most important detail.
- Single centered object filling about 85% of canvas, even margins. Front-facing view.
- Background: solid pure magenta #FF00FF, completely flat and uniform, used as a chroma-key layer.
  Do NOT draw shadows on the background.

SUBJECT:
[Custom Subject Description Here]
```
*Result:* 80+ in-game sprites were batch-processed through an automated Chroma-Key to Alpha pipeline in Unity (`SpriteImportTool.cs`), creating a seamless, cohesive retro-tech aesthetic across all tiers.

---

## 5. Verification & Quality Gates

Every component generated or refactored with AI assistance underwent strict validation:

| Test / Gate | Target | Result |
| :--- | :--- | :--- |
| **Unit Test Suite** | 31 Core & Edge Tests (`UnitTests.RunAllUnitTests()`) | **100% PASS** |
| **Soak Test (Stress)** | 3,600 consecutive simulated game frames | **0 Exceptions, 0 Memory Leaks** |
| **Garbage Collection** | Steady-state runtime allocations in gameplay loop | **0 B/frame** |
| **IL2CPP Compilation** | Android release build symbol stripping | **Clean Build (0 Warnings/Errors)** |
| **Anti-Cheat Validation**| Payload tampering & byte-level truncation tests | **100% Detection & Fallback** |

---

## 6. Key Learnings & Takeaways for Recruiters & Engineers

1. **AI is a Force Multiplier, Not an Architect:**  
   AI models excel at solving well-specified, highly bounded technical tasks (e.g. "implement geometric summation with bounds checking" or "write NUnit tests for save crypto"). Broad, vague prompts ("make an idle game") yield unmaintainable code.
   
2. **Quality Depends on Constraints:**  
   By embedding performance guidelines (zero-GC, IL2CPP compliance) and style parameters directly into prompt templates, the friction of code rework was reduced by over 80%.

3. **Accelerated Time-to-Market:**  
   Development tasks that traditionally consume weeks—such as writing exhaustive unit tests, designing mathematical progression curves, building editor tooling, and styling 80+ UI icons—were completed in days, allowing 100% of human focus to be directed toward game feel, polish, and player retention.

---
*For source code inspection, review the [Assets/Scripts](file:///C:/Users/Burak/Desktop/Application/SpeedDownload%20Idle%20Megabit%20Tycoon/Assets/Scripts) directory or examine the automated test suite in [UnitTests.cs](file:///C:/Users/Burak/Desktop/Application/SpeedDownload%20Idle%20Megabit%20Tycoon/Assets/Scripts/Editor/Tests/UnitTests.cs).*
