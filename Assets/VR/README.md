# Emil – Quest 3 VR

Scenen er Assets/Scenes/Emil.unity. VR Rig er en prefab med XR Origin, gulvbaseret tracking, VR-kamera og begge inputtyper. Den gamle Main Camera er deaktiveret. VR Test Floor er en separat prefab, som ikke ligger i scenen.

## Hænder og controllere

Spillet sætter selv Controllers-tilstand og slår skift fra, så valgene nedenfor gælder kun uden for spillet.

På VR Rig → Emil VR Input → Mode:
- Automatic: viser håndtracking, når headsettet leverer trackede hænder; ellers vises controllerhænder ved gyldig controllertracking.
- Hands: viser kun optisk trackede hænder.
- Controllers: viser kun hænder styret af controllernes position og rotation.

Venstre controllers menuknap skifter Automatic → Hands → Controllers → Automatic. Du kan stadig tage controlleren op og bruge knappen, selv om dens model er skjult i Hands-tilstand. Tilstandene vælger visningen i spillet; de ændrer ikke Quest-systemets indstillinger. Aktivér håndtracking og automatisk skift mellem hænder og controllere i headsettets indstillinger. Automatic anbefales til normal brug.

Controllerhænderne bruger samme håndmesh og materialer som de optisk trackede hænder, i en fast fingerpose. Controllerne styrer håndens position og rotation. Optiske hænder følger fingrenes bevægelser. Der er ikke tilføjet grabbing eller teleportering.

## Prefabs

Assets/VR/Prefabs indeholder:
- VR Rig
- Left Tracked Hand og Right Tracked Hand
- Left Controller Hand og Right Controller Hand
- VR Test Floor

De optiske håndmodeller stammer fra Unity XR Hands HandVisualizer-sample. De importerede kilder ligger i Assets/VR/HandVisualizer. Materialerne er tilpasset URP.

## Kør på Quest 3 fra Mac

1. Slå Developer Mode til på Quest og forbind med USB. Godkend USB-debugging i headsettet.
2. Tools → Emil VR → Switch to Quest Android (hvis Android ikke allerede er valgt).
3. Tools → Emil VR → Build Quest APK laver Builds/Emil-Quest3.apk med Emil-scenen.
4. Installer APK'en på headsettet, eller brug Unitys Build And Run med Quest valgt som Run Device.
5. Åbn appen fra headsettets appbibliotek under Unknown Sources/Ukendte kilder.

Mac-editorens Play-knap giver ikke i sig selv Quest Link VR. Brug et Android-build på Quest. Ingen headsettest er udført som del af opsætningen.

## Projektopsætning

OpenXR initialiseres ved start på Android. Meta Quest Support, Oculus Touch, Quest Touch Plus, XR Hands Hand Tracking og Meta Hand Tracking Aim er aktiveret. ARM64, IL2CPP og Vulkan er valgt. Emil er første scene i den globale buildliste; den specifikke Build Quest APK-kommando bygger kun Emil.

Tools → Emil VR → Validate Quest Setup kontrollerer Unitys OpenXR-regler for Android og skriver Assets/VR/Validation.txt. Unity tilføjer håndtrackingens Android-manifestposter gennem den aktiverede XR Hands-funktion.

## Controller tracking trails

Bemærk: Controller Tracking Trails er deaktiveret i VR Rig og er kun et debugværktøj, så sporene tegnes ikke i spillet.

Hold the RIGHT index trigger past halfway to draw both controller paths. Release it to stop; existing lines remain in the room. Press the LEFT index trigger to clear all lines. Clearing takes priority if both triggers are held. Left path is blue; right path is orange.

The trails record the controller grip position reported by OpenXR, independently of which hand visual is displayed. They require position tracking, break when tracking is reported lost, and resume as a new stroke on reacquisition. Large position jumps also start a new stroke. Quest can report estimated positions while controllers are outside camera coverage, so a line is not proof of optical tracking. Stationary samples closer than 5 mm are omitted. History is limited to 128 strokes of up to 2048 points each; oldest strokes are removed when full.

Prefabs: Controller Tracking Trails and Tracking Trail Stroke. The recorder is nested in VR Rig, with Camera Offset assigned as its tracking space.
