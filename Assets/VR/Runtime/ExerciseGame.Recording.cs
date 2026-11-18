using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace P7
{
    // Writes the session data: raw samples at 60 Hz while playing, and the summary files when the session ends.
    // Column and field names are part of the data format and must not change.
    public partial class ExerciseGame
    {
        public const string SampleHeader = "timestamp_s,exercise_time_s,exercise_id,repetition,phase,active_side,side,valid,scorable,"
            + "px,py,pz,qx,qy,qz,qw,target_px,target_py,target_pz,target_qx,target_qy,target_qz,target_qw,"
            + "head_px,head_py,head_pz,head_qx,head_qy,head_qz,head_qw,"
            + "hand_error_m,shield_normal_error_deg,head_displacement_m,head_rotation_deg,"
            + "visual_shield_px,visual_shield_py,visual_shield_pz,visual_target_px,visual_target_py,visual_target_pz";

        const string SummaryHeader = "exercise_id,side,repetition,score,complete_movement,hold_seconds,start_reached,return_reached,"
            + "endpoint_reached,valid_seconds,mean_hand_error_m,mean_shield_normal_error_deg,path_accuracy_percent,travel_coverage,"
            + "phase_completion,unpenalized_score,head_position_severity,head_penalty_points";

        const string EmptyPose = ",,,,,,,";

        bool recordingOpen;
        string sessionFolder = "";
        StreamWriter samplesCsv, samplesTsv;
        double recordingStart;
        float sampleClock;
        readonly List<string> summaryRows = new List<string>();

        // Saved as session.json: the rules and calibrations this session used.
        [Serializable]
        class SessionInfo
        {
            public bool developerMode;
            public bool handRotationScored = false;
            public string schemaVersion = "shield-6", participantId, scoreVersion = "position-only-hand-5";
            public string equipment = "shield", inputSource;
            public string calibrationMode = "automatic-estimate-or-manual-3", controlMapping = "front-translation-2";
            public string presentation = "finite-3d-paths-mirror-coach-1";
            public float exerciseGapSeconds, breakAfterSeconds, breakSeconds, sessionCapSeconds;
            public int[] challengeOrder;
            public float pathWeight = .7f, phaseWeight = .3f, stationaryScoreFactor = .25f, minimumCompletionCoverage = .65f;
            public float endpointFraction = .3f, minimumEndpointMetres = .025f, maximumEndpointMetres = .06f;
            public string anatomy = "Shoulder pivot is estimated. Elbow angle, scapular motion and torso compensation are NOT measured.";
            public Vector3 frameOrigin;
            public Quaternion frameRotation;
            public Calibration[] calibrations;
            public ExerciseInfo[] exercises;
        }

        [Serializable]
        class ExerciseInfo
        {
            public string id;
            public int repetitions;
            public float outward, hold, returnTime, rest, fullCreditMetres, zeroCreditMetres, fullCreditDegrees, zeroCreditDegrees;
        }

        void OpenRecording()
        {
            recordingOpen = true;
            replay.BeginRecording(origin, frame, simulated);
            recordingStart = Time.realtimeSinceStartupAsDouble;

            // The editor simulation only writes files when a test runs it.
            if (simulated && !TestMode)
                return;

            try
            {
                string root = simulated ? "/tmp/p7-tests" : SessionsFolder;
                sessionFolder = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
                Directory.CreateDirectory(sessionFolder);
                samplesCsv = new StreamWriter(Path.Combine(sessionFolder, "samples.csv"), false, Encoding.UTF8);
                samplesTsv = new StreamWriter(Path.Combine(sessionFolder, "samples.tsv"), false, Encoding.UTF8);
                samplesCsv.WriteLine(SampleHeader);
                samplesTsv.WriteLine(SampleHeader.Replace(',', '\t'));
            }
            catch (Exception e)
            {
                Debug.LogError("Recording failed: " + e.Message);
            }
        }

        void RecordAt60Hz(bool tracked, Vector3 target, Quaternion targetRotation)
        {
            sampleClock += Time.unscaledDeltaTime;
            if (sampleClock < 1f / 60)
                return;
            sampleClock %= 1f / 60;
            Record(tracked, target, targetRotation);
        }

        void Record(bool tracked, Vector3 target, Quaternion targetRotation)
        {
            var profile = ActiveProfile;
            int activeSide = ActiveSide;
            string phase = profile.Phase(clock);
            bool headTracked = HeadTracked();
            float time = (float)(Time.realtimeSinceStartupAsDouble - recordingStart);

            // Only the active arm has a target; the other arm's target is its own position.
            Vector3 leftTarget = activeSide == 0 ? target : handPosition[0];
            Vector3 rightTarget = activeSide == 1 ? target : handPosition[1];
            replay.Capture(new ReplayFrame
            {
                time = time,
                exerciseId = profile.exerciseId,
                phase = phase,
                activeSide = activeSide,
                headValid = headTracked,
                leftValid = handTracked[0],
                rightValid = handTracked[1],
                head = head.position,
                headRotation = head.rotation,
                left = handPosition[0],
                right = handPosition[1],
                leftRotation = handRotation[0],
                rightRotation = handRotation[1],
                targetLeft = leftTarget,
                targetRight = rightTarget,
                targetLeftRotation = activeSide == 0 ? targetRotation : handRotation[0],
                targetRightRotation = activeSide == 1 ? targetRotation : handRotation[1],
            });

            if (samplesCsv == null)
                return;

            try
            {
                for (int s = 0; s < 2; s++)
                {
                    string row = SampleRow(s, tracked, headTracked, time, phase, target, targetRotation);
                    samplesCsv.WriteLine(row);
                    samplesTsv.WriteLine(row.Replace(',', '\t'));
                }
                if (Time.frameCount % 60 == 0)
                {
                    samplesCsv.Flush();
                    samplesTsv.Flush();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Recording failed: " + e.Message);
                CloseSampleFiles();
            }
        }

        string SampleRow(int side, bool tracked, bool headTracked, float time, string phase, Vector3 target, Quaternion targetRotation)
        {
            int activeSide = ActiveSide;
            bool active = side == activeSide;
            bool valid = handTracked[side];

            var row = new StringBuilder();
            row.Append(F(time)).Append(',').Append(F(clock)).Append(',').Append(ActiveProfile.exerciseId)
                .Append(',').Append(repetition + 1).Append(',').Append(phase)
                .Append(',').Append(SideId(activeSide)).Append(',').Append(SideId(side))
                .Append(',').Append(valid ? 1 : 0).Append(',').Append(tracked && active ? 1 : 0);

            AppendPose(row, valid, handPosition[side], handRotation[side]);
            AppendPose(row, active, target, targetRotation);
            AppendPose(row, headTracked, head.position, head.rotation);

            bool measured = active && valid;
            row.Append(',').Append(measured ? F(Vector3.Distance(handPosition[side], target)) : "");
            row.Append(',').Append(measured ? F(Vector3.Angle(handRotation[side] * Vector3.forward, targetRotation * Vector3.forward)) : "");
            row.Append(',').Append(headTracked ? F(Vector3.Distance(head.position, origin)) : "");
            row.Append(',').Append(headTracked ? F(Quaternion.Angle(head.rotation, neutralHead)) : "");

            if (measured)
            {
                Vector3 shown = MapToFront(handPosition[side], side);
                Vector3 shownTarget = MapToFront(target, side);
                row.Append(',').Append(F(shown.x)).Append(',').Append(F(shown.y)).Append(',').Append(F(shown.z));
                row.Append(',').Append(F(shownTarget.x)).Append(',').Append(F(shownTarget.y)).Append(',').Append(F(shownTarget.z));
            }
            else
            {
                row.Append(",,,,,,");
            }
            return row.ToString();
        }

        static void AppendPose(StringBuilder row, bool include, Vector3 p, Quaternion q)
        {
            if (!include)
            {
                row.Append(EmptyPose);
                return;
            }
            row.Append(',').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z));
            row.Append(',').Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w));
        }

        void CloseSampleFiles()
        {
            samplesCsv?.Dispose();
            samplesTsv?.Dispose();
            samplesCsv = samplesTsv = null;
        }

        void AddSummaryRow(ExerciseProfile profile, int side, float score, bool complete, float pathQuality, float coverage,
            float phases, float rawScore, float headSeverity)
        {
            var r = current;
            summaryRows.Add(string.Join(",",
                profile.exerciseId, SideId(side), repetition + 1, F(score * 100), Bit(complete), F(r.heldSeconds),
                Bit(r.startReached), Bit(r.returnReached), Bit(r.endpointReached), F(r.validSeconds),
                F(r.MeanError), F(r.MeanAngle), F(pathQuality * 100), F(coverage), F(phases), F(rawScore * 100),
                F(headSeverity), F((rawScore - score) * 100)));
        }

        void WriteSessionFiles(bool complete)
        {
            try
            {
                string participantId = TestMode ? "TEST" : Participant.Id;
                var run = pathFeedback.Run;

                Save("conduit.json", JsonUtility.ToJson(run, true));
                SaveTable("conduit", pathFeedback.ExportProgress());
                SaveTable("conduit-path", pathFeedback.ExportNodes());

                Save("game-points.json", "{\"version\":\"perfect-flow-2\",\"total_points\":" + scoreFeedback.Points
                    + ",\"tracking_bonus_points\":" + scoreFeedback.TrackingBonusPoints
                    + ",\"warmup_seconds\":2,\"base_rep_points\":1000,\"base_tick_points\":100,\"tick_seconds\":0.5,\"minimum_quality\":0.9}");
                Save("adaptive-coach.json", adaptiveCoach.ExportJson());
                Save("session.json", JsonUtility.ToJson(BuildSessionInfo(participantId), true));
                SaveTable("summary", SummaryHeader + "\n" + string.Join("\n", summaryRows));
                SaveTable("therapist-view", TherapistView.Csv(RepLog));  // added 2026-11-18, a new file only
                Save("outcome.json", "{\"participant_id\":\"" + participantId + "\",\"complete\":" + (complete ? "true" : "false")
                    + ",\"outcome\":\"" + run.outcome + "\",\"tracking_pause_seconds\":" + F(trackingPause)
                    + ",\"active_seconds\":" + F(activeSeconds) + ",\"breaks\":" + breaksTaken
                    + ",\"off_spot_seconds\":" + F(offSpotSeconds) + "}");
                replay.Save(sessionFolder);
            }
            catch (Exception e)
            {
                Debug.LogError("Save failed: " + e.Message);
            }
        }

        SessionInfo BuildSessionInfo(string participantId)
        {
            return new SessionInfo
            {
                participantId = participantId,
                developerMode = Settings.devMode,
                exerciseGapSeconds = exerciseGapSeconds,
                breakAfterSeconds = breakAfterSeconds,
                breakSeconds = breakSeconds,
                sessionCapSeconds = sessionCapSeconds,
                challengeOrder = playlist.ToArray(),
                inputSource = simulated ? "SIMULATED TEST" : "controller-grip",
                frameOrigin = origin,
                frameRotation = frame,
                calibrations = calibrations.ToArray(),
                exercises = Array.ConvertAll(exercises, e => new ExerciseInfo
                {
                    id = e.exerciseId,
                    repetitions = e.repetitions,
                    outward = e.outwardSeconds,
                    hold = e.holdSeconds,
                    returnTime = e.returnSeconds,
                    rest = e.restSeconds,
                    fullCreditMetres = e.fullCreditMetres,
                    zeroCreditMetres = e.zeroCreditMetres,
                    fullCreditDegrees = e.fullCreditDegrees,
                    zeroCreditDegrees = e.zeroCreditDegrees,
                }),
            };
        }

        void Save(string fileName, string text) => File.WriteAllText(Path.Combine(sessionFolder, fileName), text);

        // Every table is saved both comma- and tab-separated.
        void SaveTable(string name, string csv)
        {
            Save(name + ".csv", csv);
            Save(name + ".tsv", csv.Replace(',', '\t'));
        }

        static string SideId(int side) => side == 0 ? "left" : "right";

        static string Bit(bool value) => value ? "1" : "0";

        static string F(double value) => value.ToString("G9", CultureInfo.InvariantCulture);
    }
}
