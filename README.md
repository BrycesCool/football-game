# Gridiron — Unity Football Game

A work-in-progress American football game built in Unity.

## Tech

- Unity (Universal Render Pipeline) with separate PC and Mobile renderer setups
- Unity Input System
- Edit Mode unit tests (NUnit)

## Project structure

- `Assets/_Game/` — core game code and content (`Scripts`, `Prefabs`, `Scenes`, `Data`, `Tests`)
- `Assets/Animations/` — character animations (quarterback throw, catches, running, turns)
- `Assets/Character/` — player and football models
- `Assets/Stadium/` — stadium models, materials, and textures
- `Assets/Scenes/` — sample scene
- `Assets/Settings/` — render pipeline assets

## Getting started

1. Install **Unity Hub** and the Editor version listed in `ProjectSettings/ProjectVersion.txt`.
2. Clone the repo:
   ```
   git clone https://github.com/BrycesCool/football-game.git
   ```
3. Open the project folder in Unity Hub.

## Running tests

In the Editor: **Window > General > Test Runner**, then run the Edit Mode tests.

## Notes

The `Library/`, `Temp/`, `Logs/`, and build folders are intentionally excluded from version control — Unity regenerates them automatically when the project is opened.
