# Project Overview

- **Game Title**: Summer Night Cyber Fireworks
- **High-Level Concept**: A highly stylized, retro-synthwave 2D scene of a summer beach at night, featuring reactive cyber fireworks that glow with vibrant HDR Bloom on click/tap, paired with ambient procedural ocean audio.
- **Players**: Single player (interactive toy/relaxing sandbox)
- **Inspiration / Reference Games**: Synthwave / OutRun aesthetics, interactive desktop relaxers, fluid particle sandboxes.
- **Tone / Art Direction**: Cyberpunk / Synthwave neon style. Dark deep space purples (#0a0618, #241259) contrasted against ultra-bright neon pinks, cyans, and greens. Intense HDR glows.
- **Target Platform**: PC / WebGL
- **Screen Orientation / Resolution**: Landscape (1920x1080)
- **Render Pipeline**: Universal Render Pipeline (URP) with 2D Renderer & post-processing.

---

# Game Mechanics

## Core Gameplay Loop
The player watches a relaxing beach scene where cyber fireworks automatically launch and detonate in the sky at random intervals (1.1s to 2.2s). The player can also interactively tap/click anywhere on the screen to instantly launch and detonate a firework at that exact position. Each explosion triggers vibrant particle bursts that illuminate the sky, glowing with intense Bloom effects, accompanied by synced launch/explosion sound effects.

## Controls and Input Methods
- **Input Backend**: Unity New Input System.
- **Mouse / Touch Click**: Spawns a firework at the tapped/clicked screen position. The screen position is projected into world coordinates for precise spawning.
- **On-Screen Auto-Spawning**: Hands-free mode that automatically launches fireworks at random coordinates to keep the scene active and dynamic.

---

# UI
- **HUD / Interface**: Highly minimalistic or completely borderless. A small toggle or instructions text can be added using TextMesh Pro, styled with a glowing neon purple/cyan outline matching the synthwave style.
- **Aesthetic**: All text uses clean sans-serif fonts (e.g. Inter or Liberation Sans) with emissive materials/effects to blend seamlessly into the background.

---

# Key Asset & Context

### 1. URP Asset & Renderer Configuration
- **URP Asset**: `CyberFireworks_URPAsset.asset`
- **Renderer**: `CyberFireworks_Renderer2D.asset` (Renderer2DData) for 2D lighting, or `CyberFireworks_UniversalRenderer.asset` (UniversalRendererData) for Universal post-processing features.
- **Volume Profile**: `CyberFireworks_VolumeProfile.asset` with a **Bloom** override (Threshold = 0.9, Intensity = 1.2).

### 2. Scene: `SummerNightBeach.unity`
- **Sorting Layers**:
  1. `Sky` (Order in Layer 0)
  2. `Sea` (Order in Layer 10)
  3. `Fireworks` (Order in Layer 20)

### 3. Shaders
- **Sky Shader** (`Shader Graph` Unlit):
  - Synthwave vertical gradient (#0a0618 → #241259 → #7a1f6b)
  - Emissive half-circle sun (gradient #ffd23f → #ff2fd0) at the center-bottom horizon
  - Moving horizontal scanlines across sky and sun
- **Sea Shader** (`Shader Graph` Unlit):
  - Base depth gradient (#3a1170 → #141a5c → #050a2e)
  - Scrolling perspective grid (converging to horizon, scrolling downwards)
  - 3-4 layered animated sine wave lines in emissive light-blue HDR

### 4. Scripts
- **`FireworkLauncher.cs`**: Handles click/tap detection via New Input System and auto-launch timers.
- **`OceanAmbienceModulator.cs`**: Modulates looping AudioSource volume using two independent slow sine LFOs.

---

# Implementation Steps

### Step 1: Create and Assign URP Assets
- **Description**: 
  - Create a 2D Renderer asset `Assets/SourceFiles/Settings/CyberFireworks_Renderer2D.asset` and a Universal Renderer asset `Assets/SourceFiles/Settings/CyberFireworks_UniversalRenderer.asset`.
  - Create the URP Asset `Assets/SourceFiles/Settings/CyberFireworks_URPAsset.asset` referencing these renderers.
  - Set the active render pipeline in `Project Settings > Graphics` to `CyberFireworks_URPAsset`.
  - Enable HDR in the URP Asset.
  - Create a Volume Profile `Assets/SourceFiles/Settings/CyberFireworks_VolumeProfile.asset` and add a `Bloom` override with `Threshold = 0.9` and `Intensity = 1.2`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

### Step 2: Set up Scene, Camera, and Global Volume
- **Description**:
  - Create a new 2D scene at `Assets/Scenes/SummerNightBeach.unity`.
  - Set Main Camera clear flags to Solid Color (#0a0618), Orthographic Projection, and enable Post-Processing (`renderPostProcessing = true` in `UniversalAdditionalCameraData`).
  - Create a GameObject named `Global Volume` with a `Volume` component, set `isGlobal = true`, and assign the `CyberFireworks_VolumeProfile`. Ensure its layer is inside the Camera's volume layer mask.
  - Configure Sorting Layers: `Sky`, `Sea`, `Fireworks`.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 3: Implement the Synthwave Sky System
- **Description**:
  - Create a Quad or Sprite GameObject named `Sky` assigned to the `Sky` sorting layer.
  - Create a custom Unlit Shader Graph `SynthwaveSkyShader` that draws the vertical background gradient, half-circle sun with its yellow-to-pink gradient, and scrolling scanlines. Use HDR output for emissive components.
  - Create a material `Sky_Mat` using this shader and apply it to the `Sky` GameObject.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

### Step 4: Implement the Sea System
- **Description**:
  - Create a Quad/Sprite GameObject named `Sea` assigned to the `Sea` sorting layer, positioned below the sky's horizon.
  - Create a custom Unlit Shader Graph `SynthwaveSeaShader` that draws the background sea gradient, the scrolling perspective grid, and 3-4 sine wave lines with high HDR light-blue emission.
  - Create a material `Sea_Mat` using this shader and apply it to the `Sea` GameObject.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

### Step 5: Design the Fireworks Particle System
- **Description**:
  - Create a Shuriken Particle System Prefab `CyberFirework_ParticleSystem.prefab` in `Assets/Prefabs/`.
  - Set the system to launch a single rocket particle upward and trigger a sub-emitter on its death, bursting into ~50 radial particles.
  - Assign random HDR colors (#00f0ff, #ff2fd0, #7b5bff, #39ff9e) to the burst particles.
  - Use an Additive, HDR-compatible particle material to enable intense neon Bloom glow.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: Yes

### Step 6: Create the Launch Controller Script
- **Description**:
  - Create a C# script `FireworkLauncher.cs` under `Assets/SourceFiles/Scripts/`.
  - Add logic to capture tap/click input, project to world space, and instantiate the firework prefab.
  - Implement the auto-launch timer with randomized delays (1.1s to 2.2s).
  - Add a Launcher GameObject in the scene and attach this script.
- **Assigned role**: developer
- **Dependencies**: Step 5
- **Parallelizable**: Yes

### Step 7: Audio and Volume Modulation Setup
- **Description**:
  - Add an AudioSource to a new `AmbientAudio` GameObject, set to Loop, and reference `SFX_AmbienceClose.ogg`.
  - Create a C# script `OceanAmbienceModulator.cs` that dynamically changes the volume of this AudioSource via two sine LFOs (different low frequencies).
  - Modify `FireworkLauncher.cs` to trigger rocket launch and burst explosion SFX (using existing audio clips like `SFX_PositiveSound01.ogg` or other suitable sounds from `Assets/SourceFiles/SoundFX/`).
- **Assigned role**: developer
- **Dependencies**: Step 6
- **Parallelizable**: Yes

---

# Verification & Testing

## 1. Automated/Console Verifications
- Run a C# script to perform pre-flight checks:
  - Verify URP asset is active and HDR is enabled.
  - Verify Main Camera has `renderPostProcessing = true`.
  - Verify Global Volume is active, has `isGlobal = true`, has the profile assigned, and the profile contains a `Bloom` override with threshold ~0.9 and intensity ~1.2.
  - Verify there are no null reference exceptions or compile errors in the console.

## 2. Interactive Manual Checks
- **Post-Processing Glow Check**: Look at the Game View; the sun, sea wave lines, and fireworks particles must glow with a vibrant neon blur. If there is no glow, check camera post-processing toggles and volume layer masks.
- **Input Responsiveness Check**: Enter Play Mode and tap/click multiple locations on the screen. A firework prefab must immediately instantiate at the exact cursor position.
- **LFO Audio Volume Check**: Listen to the looping background ambient waves in Play Mode. The volume should rise and fall naturally and smoothly without sudden cuts or absolute silence.
