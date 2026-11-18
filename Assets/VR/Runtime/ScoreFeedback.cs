using UnityEngine;

namespace P7
{
    // Points, streaks, sounds and the floating score display.
    public class ScoreFeedback : MonoBehaviour
    {
        // A major pentatonic from the 880 Hz completion tone, so a perfect repetition never sounds lower than a complete one.
        static readonly float[] StreakHz = { 880, 987.77f, 1108.73f, 1318.51f, 1479.98f, 1760 };
        const int SampleRate = 22050;
        const int LineHz = 220;
        const float LineLevelOff = .012f, LineLevelOn = .035f;
        const float LineCutoffOff = 300, LineCutoffOn = 1600;
        const float LineFadeSeconds = .2f;
        const float ChimeDelay = .2f;

        static readonly Color Ice = new Color(.35f, .85f, 1);
        static readonly Color Gold = new Color(1, .84f, .35f);
        static readonly Color RailColor = new Color(.06f, .15f, .2f);
        static readonly Color FlowColor = new Color(.5f, .65f, .75f);

        public Transform completionRing;
        // Only used for its font and material, which the runtime text copies.
        public TextMesh fontSource;

        public int Points { get; private set; }
        public int TrackingBonusPoints { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Multiplier => Mathf.Clamp(Combo, 1, 5);
        public readonly PerfectFlowBonus TrackingBonus = new PerfectFlowBonus();

        ExerciseGame game;

        AudioSource sound;
        AudioClip captureTone, endpointTone, completeTone, multiplierChime;
        AudioClip[] streakNotes, bonusDings;
        AudioSource lineSource;
        AudioLowPassFilter lineFilter;
        AudioClip lineClip;
        float lineFade, lineQuality;

        Transform hud;
        TextMesh scoreText, streakText, completionText, trackingWarning, bonusText;
        LineRenderer energyFill, flowFill, bonusHalo;
        float shownPoints, scorePulse, completionTime, bonusPulse, ringTime;

        // Team decision 2026-11-04: perfect = complete, score >= 90 and coverage >= 90 %.
        // Added 2026-11-18: the target ring must also have been green for PerfectGreenShare of the scored time, so the
        // colour and the reward agree (before, a rep with the ring yellow 22 % of the time could still be PERFECT).
        public const float PerfectGreenShare = .9f;

        public static bool Perfect(bool complete, float score, float coverage, float greenShare = 1)
            => complete && score >= .9f && coverage >= .9f && greenShare >= PerfectGreenShare;

        public static int StreakNote(int combo) => Mathf.Clamp(combo - 1, 0, StreakHz.Length - 1);

        public static float LineLevel(float quality) => Mathf.Lerp(LineLevelOff, LineLevelOn, quality);

        public static float LineCutoff(float quality) => LineCutoffOff * Mathf.Pow(LineCutoffOn / LineCutoffOff, Mathf.Clamp01(quality));

        void Awake()
        {
            game = GetComponentInParent<ExerciseGame>();
            CreateSounds();
        }

        void OnDisable()
        {
            StopLineTone();
            TrackingBonus.Reset();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                StopLineTone();
        }

        void OnDestroy()
        {
            Destroy(captureTone);
            Destroy(endpointTone);
            Destroy(completeTone);
            Destroy(multiplierChime);
            Destroy(lineClip);
            foreach (var clip in streakNotes) Destroy(clip);
            foreach (var clip in bonusDings) Destroy(clip);
        }

        public void ResetMission()
        {
            TrackingBonus.Reset();
            Points = TrackingBonusPoints = Combo = BestCombo = 0;
            shownPoints = scorePulse = completionTime = bonusPulse = ringTime = 0;
        }

        public void Captured() => sound.PlayOneShot(captureTone);

        public void EndpointReached(Vector3 point)
        {
            sound.PlayOneShot(endpointTone);
            completionRing.position = point;
            ringTime = .5f;
        }

        // Called at the end of every repetition.
        public void Reward(bool complete, bool perfect, float quality)
        {
            scorePulse = 1;
            completionTime = complete ? 1.4f : 0;
            if (completionText)
                completionText.text = perfect ? "PERFECT" : "NICE";

            int multiplierBefore = Multiplier;
            int basePoints = Mathf.RoundToInt(1000 * quality);
            Points += basePoints;

            if (complete && !perfect)
                sound.PlayOneShot(completeTone);

            if (perfect)
            {
                Combo++;
                BestCombo = Mathf.Max(BestCombo, Combo);
                Points += basePoints * Mathf.Min(Combo - 1, 4);
                sound.PlayOneShot(streakNotes[StreakNote(Combo)]);
                if (Multiplier > multiplierBefore)
                    sound.PlayOneShot(multiplierChime);
            }
            else
            {
                Combo = 0;
            }
        }

        // Called every frame while playing.
        public void TrackPerfect(bool eligible, float quality, float deltaTime)
        {
            int points = TrackingBonus.Advance(eligible, quality, deltaTime);
            if (points == 0)
                return;

            Points += points;
            TrackingBonusPoints += points;
            bonusPulse = 1;
            scorePulse = 1;
            sound.PlayOneShot(bonusDings[TrackingBonus.Multiplier - 1], .8f);
        }

        // A soft tone while the hand is on the line. It gets quieter and duller the further off the line the hand is.
        public void LineTone(bool on, float quality)
        {
            if (!on && lineFade <= 0)
                return;

            float step = Time.unscaledDeltaTime / LineFadeSeconds;
            lineFade = Mathf.MoveTowards(lineFade, on ? 1 : 0, step);
            lineQuality = Mathf.MoveTowards(lineQuality, quality, step);
            lineSource.volume = lineFade * LineLevel(lineQuality);
            lineFilter.cutoffFrequency = LineCutoff(lineQuality);

            if (lineFade <= 0)
                lineSource.Stop();
            else if (!lineSource.isPlaying)
                lineSource.Play();
        }

        public void StopLineTone()
        {
            lineFade = 0;
            if (lineSource)
            {
                lineSource.volume = 0;
                lineSource.Stop();
            }
        }

        void Update()
        {
            ringTime = Mathf.Max(0, ringTime - Time.unscaledDeltaTime);
            completionRing.gameObject.SetActive(ringTime > 0);
            if (ringTime > 0)
                completionRing.localScale = Vector3.one * (.2f + (1 - ringTime / .5f) * .5f);
        }

        void LateUpdate()
        {
            if (!hud)
                CreateHud();

            bool visible = game.PathsVisible && !game.replay.IsActive;
            hud.gameObject.SetActive(visible);
            hud.SetPositionAndRotation(game.Origin + game.Frame * new Vector3(0, .10f, 2.8f), game.Frame);

            float dt = Time.unscaledDeltaTime;
            float countSpeed = Mathf.Max(3000, Mathf.Abs(Points - shownPoints) * 9);
            shownPoints = Mathf.MoveTowards(shownPoints, Points, countSpeed * dt);
            scorePulse = Mathf.Max(0, scorePulse - dt * 3);
            completionTime = Mathf.Max(0, completionTime - dt);
            bonusPulse = Mathf.Max(0, bonusPulse - dt * 4);

            scoreText.text = Mathf.RoundToInt(shownPoints).ToString("N0");
            scoreText.transform.localScale = Vector3.one * (1 + .06f * scorePulse);
            scoreText.color = Color.Lerp(new Color(.8f, .9f, .96f), Color.white, scorePulse);
            streakText.text = Combo > 1 ? "<b>" + Multiplier + "x</b>  /  " + Combo + " STREAK" : "";

            float completion = Mathf.Clamp01(completionTime);
            completionText.gameObject.SetActive(completionTime > 0);
            completionText.color = Color.Lerp(new Color(.25f, .25f, .22f), Gold, completion);
            completionText.transform.localPosition = new Vector3(0, .19f + .035f * (1 - completion), 0);

            trackingWarning.gameObject.SetActive(visible && !game.IsTracked);

            SetBar(energyFill, -.18f, game.pathFeedback.Run.power);
            SetBar(flowFill, -.24f, game.pathFeedback.Run.battery / 100);

            UpdateBonusDisplay(visible && game.IsPlaying && TrackingBonus.Ticks > 0);
        }

        // A gold ring and "+points" above the target while the perfect-flow bonus is running.
        void UpdateBonusDisplay(bool show)
        {
            bonusText.gameObject.SetActive(show);
            bonusHalo.gameObject.SetActive(show && bonusPulse > 0);
            if (!show)
                return;

            Vector3 target = game.targetMarker.position;
            bonusHalo.transform.SetPositionAndRotation(target, game.Frame);
            float radius = .09f + (1 - bonusPulse) * .045f;
            for (int i = 0; i < bonusHalo.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / bonusHalo.positionCount;
                bonusHalo.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -.008f));
            }
            bonusHalo.widthMultiplier = .003f * bonusPulse;

            Vector3 above = new Vector3(0, .17f + .015f * bonusPulse, -.015f);
            bonusText.transform.SetPositionAndRotation(target + game.Frame * above, game.Frame);
            bonusText.transform.localScale = Vector3.one * (1 + .13f * bonusPulse);
            bonusText.text = "+" + TrackingBonus.StreakPoints + "   x" + TrackingBonus.Multiplier + "\n<size=24>PERFECT FLOW</size>";
        }

        void CreateHud()
        {
            hud = new GameObject("Floating score HUD").transform;
            hud.SetParent(transform, false);

            scoreText = CreateText("Session points", hud, Vector3.zero, .022f, new Color(.65f, .78f, .85f));
            streakText = CreateText("Rep streak", hud, new Vector3(0, -.12f, 0), .007f, Ice);
            completionText = CreateText("Completion accent", hud, new Vector3(0, .19f, 0), .009f, Gold);
            trackingWarning = CreateText("Tracking warning", hud, new Vector3(0, -.33f, 0), .007f, Gold);
            trackingWarning.text = "TRACKING PAUSED";
            CreateText("Energy label", hud, new Vector3(-.39f, -.18f, 0), .0055f, Ice).text = "ENERGY";
            CreateText("Flow label", hud, new Vector3(-.39f, -.24f, 0), .0055f, FlowColor).text = "FLOW";

            SetBar(CreateLine("Energy rail", hud, .006f, RailColor), -.18f, 1);
            SetBar(CreateLine("Flow rail", hud, .004f, RailColor), -.24f, 1);
            energyFill = CreateLine("Energy progress", hud, .009f, Ice);
            flowFill = CreateLine("Flow remaining", hud, .005f, FlowColor);
            energyFill.transform.localPosition = flowFill.transform.localPosition = Vector3.back * .002f;

            bonusText = CreateText("Perfect flow bonus", transform, Vector3.zero, .008f, new Color(1, .86f, .4f));
            bonusHalo = CreateLine("Bonus pulse ring", transform, .003f, Gold);
            bonusHalo.loop = true;
            bonusHalo.positionCount = 48;
        }

        TextMesh CreateText(string textName, Transform parent, Vector3 position, float size, Color color)
        {
            var go = new GameObject(textName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var text = go.AddComponent<TextMesh>();
            text.font = fontSource.font;
            text.GetComponent<Renderer>().sharedMaterial = fontSource.GetComponent<Renderer>().sharedMaterial;
            text.fontSize = 64;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            return text;
        }

        LineRenderer CreateLine(string lineName, Transform parent, float width, Color color)
        {
            var line = new GameObject(lineName).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = game.pathStream.paths[0].sharedMaterial;
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            line.SetPropertyBlock(block);
            return line;
        }

        // A horizontal bar under the score, filled to the given fraction.
        static void SetBar(LineRenderer line, float y, float fraction)
        {
            const float left = -.28f, width = .56f;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(left, y, 0));
            line.SetPosition(1, new Vector3(left + Mathf.Max(.001f, width * fraction), y, 0));
        }

        void CreateSounds()
        {
            sound = gameObject.AddComponent<AudioSource>();
            sound.spatialBlend = 0;
            sound.volume = .18f;

            captureTone = Tone(660, .16f);
            endpointTone = Tone(330, .12f);
            completeTone = Tone(880, .28f);
            multiplierChime = Chime();

            streakNotes = new AudioClip[StreakHz.Length];
            for (int i = 0; i < StreakHz.Length; i++)
                streakNotes[i] = Tone(StreakHz[i], .28f);

            bonusDings = new AudioClip[5];
            for (int i = 0; i < bonusDings.Length; i++)
                bonusDings[i] = Ding(StreakHz[i]);

            // A low-pass filter affects every source on its GameObject, so the line tone gets its own child.
            var lineObject = new GameObject("Line Tone");
            lineObject.transform.SetParent(transform, false);
            lineClip = LineToneClip();
            lineSource = lineObject.AddComponent<AudioSource>();
            lineSource.playOnAwake = false;
            lineSource.loop = true;
            lineSource.spatialBlend = 0;
            lineSource.volume = 0;
            lineSource.clip = lineClip;
            lineFilter = lineObject.AddComponent<AudioLowPassFilter>();
        }

        static AudioClip Tone(float frequency, float seconds)
        {
            int count = (int)(SampleRate * seconds);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float envelope = Mathf.Sin(Mathf.PI * i / count);
                data[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / SampleRate) * envelope * .35f;
            }
            return Clip("Tone", data);
        }

        static AudioClip Ding(float frequency)
        {
            var data = new float[6615];
            for (int i = 0; i < data.Length; i++)
                data[i] = Bell(i / (float)SampleRate, frequency) * .55f;
            return Clip("Perfect flow ding", data);
        }

        // Two bell notes. The silence at the start lets the streak note play first.
        static AudioClip Chime()
        {
            var data = new float[(int)(SampleRate * (ChimeDelay + .5f))];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate - ChimeDelay;
                data[i] = (Bell(t, 1318.51f) + Bell(t - .09f, 1760)) * .28f;
            }
            return Clip("Multiplier chime", data);
        }

        static float Bell(float t, float frequency)
        {
            if (t < 0)
                return 0;
            return Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Min(1, t / .004f) * Mathf.Exp(-t * 12);
        }

        // One second with a whole number of cycles, so the loop does not click. The overtones give the filter something to remove.
        static AudioClip LineToneClip()
        {
            var data = new float[SampleRate];
            for (int i = 0; i < data.Length; i++)
            {
                float w = 2 * Mathf.PI * LineHz * i / SampleRate;
                data[i] = (Mathf.Sin(w) + .4f * Mathf.Sin(2 * w) + .2f * Mathf.Sin(3 * w) + .1f * Mathf.Sin(4 * w)) / 1.7f;
            }
            return Clip("Line tone", data);
        }

        static AudioClip Clip(string clipName, float[] data)
        {
            var clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
