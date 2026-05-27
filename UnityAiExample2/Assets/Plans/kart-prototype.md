# Project Overview
- Game Title: Arcade Kart Racer (Mario Kart Style)
- High-Level Concept: A fluid, optimized arcade kart racer with snappy controls and stable physics on bumpy terrain.
- Players: Single player (base setup).
- Inspiration / Reference Games: Mario Kart, Crash Team Racing.
- Tone / Art Direction: Arcade, vibrant.
- Target Platform: Standalone PC (Windows).
- Screen Orientation / Resolution: Landscape 1920x1080.
- Render Pipeline: URP (Universal Render Pipeline).

# Game Mechanics
## Core Gameplay Loop
- Players accelerate and steer a kart over bumpy terrain.
- The focus is on maintaining speed and handling the bumps smoothly.

## Controls and Input Methods
- **Accelerate:** (W / Up Arrow / Gamepad R2) - Constant forward force.
- **Brake/Reverse:** (S / Down Arrow / Gamepad L2) - Braking force or reverse movement.
- **Steer:** (A/D / Left/Right Arrow / Gamepad Left Stick) - Yaw rotation based on speed.

# UI
- For now, no UI is requested. The focus is on the gameplay feel.

# Key Asset & Context
- **Scripts:**
    - `KartController.cs`: Handles physics, forces, and alignment.
    - `SmoothFollowCamera.cs`: Handles camera interpolation and positioning.
    - `GroundGenerator.cs`: (Optional/Simple) Creates a bumpy mesh for testing.
- **Input:**
    - `KartActions.inputactions`: Defines mappings for driving.
- **Prefabs:**
    - `Kart_Player`: Sphere Rigidbody with visual child.

# Implementation Steps
## 1. Environment Setup
- Create a new scene `Assets/Scenes/KartTestScene.unity`.
- Generate a bumpy ground: Use a simple plane with a `MeshCollider` or a script-generated Perlin noise mesh.
- **Dependency:** None.

## 2. Input Configuration
- Create `Assets/Settings/KartActions.inputactions`.
- Define Action Map `Driving`:
    - `Accelerate`: Value (Axis)
    - `Steer`: Value (Axis)
- **Dependency:** Step 1.

## 3. Vehicle Physics (Sphere + Raycasts)
- Create the Kart GameObject:
    - Root: `Rigidbody` (Mass ~1500), `SphereCollider` (Frictionless material).
    - Child `Visuals`: Holds the kart model.
- Implement `KartController.cs`:
    - Apply `AddForce` to the Rigidbody for acceleration/braking.
    - Align `Visuals` rotation to ground normal using raycasts (Front, Back, Left, Right or just Center).
    - Smoothly interpolate the visual alignment for stability.
- **Dependency:** Step 2.

## 4. Camera System
- Implement `SmoothFollowCamera.cs`:
    - Target: Kart root.
    - Offset: Distance and height from target.
    - Smoothing: `Vector3.SmoothDamp` for position and `Quaternion.Slerp` for rotation.
- **Dependency:** Step 3.

## 5. Tweak & Refine
- Expose variables: `acceleration`, `steeringSpeed`, `alignmentSpeed`, `cameraSmoothness`.
- Ensure the sphere doesn't get stuck on bumps (adjust friction and gravity).

# Verification & Testing
- **Movement:** Verify the kart accelerates and brakes correctly.
- **Steering:** Verify steering responsiveness at different speeds.
- **Terrain:** Verify the kart handles bumps without flipping or jittering.
- **Camera:** Verify smooth movement without stuttering.
