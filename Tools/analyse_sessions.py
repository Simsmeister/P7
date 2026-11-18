"""Analyse P7 session exports for the final test.

Usage:  python analyse_sessions.py <Sessions folder> [<Sessions folder> ...] [--out DIR]
        python analyse_sessions.py --selftest

Writes sessions.csv, cells.csv (participant x session x exercise x arm), summary.md and, if matplotlib
is installed, one PNG of range/speed per participant x session x exercise x arm.

Definitions (proposals, check with the team):
- speed = 1 / durationScale (the coach logs a duration scale; the therapist view shows 1/duration).
- PERFECT = therapist-view.csv `perfect` when present, else complete and score >= 90 and coverage >= .9
  (newer builds also need green_share >= .9, which older exports cannot reproduce).
- learned = reason is "Network increases...", "Network eases..." or "Maintain difficulty"
  (same rule as TherapistView.Rep.Learned).
- settled = in the last third (rounded up, at least 2) of that cell's learned coach decisions, every range change
  is under .02 and every speed change under .0225 (relative), i.e. inside the coach's dead zone.
- Sessions with developer mode on or < 1 min active play are flagged and left out of cells.csv.
"""
import csv, json, os, sys, tempfile
from collections import defaultdict

MIN_ACTIVE_S = 60
RANGE_DZ, SPEED_DZ = .02, .0225
LEARNED = ("Network increases", "Network eases", "Maintain difficulty")


def load_json(path):
    try:
        with open(path, encoding="utf-8-sig") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def load_csv(path):
    try:
        with open(path, encoding="utf-8-sig", newline="") as f:
            return list(csv.DictReader(f))
    except OSError:
        return None


def num(v, default=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return default


def kind(d):
    # Classify by the logged level change, not the reason text: the coach logs "increases" even when
    # range and speed are already at their caps (1.0 / 1.4) and nothing changes.
    if not (d.get("reason") or "").startswith(LEARNED):
        return "not_learned"
    r = num(d.get("nextRangeScale"), 1) / num(d.get("rangeScaleUsed"), 1) - 1
    s = num(d.get("durationScaleUsed"), 1) / num(d.get("nextDurationScale"), 1) - 1
    if r < -1e-4 or s < -1e-4: return "ease"
    if r > 1e-4 or s > 1e-4: return "increase"
    return "keep"


def read_session(folder):
    name = os.path.basename(os.path.normpath(folder))
    problems = []
    sess = load_json(os.path.join(folder, "session.json")) or {}
    out = load_json(os.path.join(folder, "outcome.json")) or {}
    reps = load_csv(os.path.join(folder, "summary.csv"))
    coach = load_json(os.path.join(folder, "adaptive-coach.json")) or {}
    tv = load_csv(os.path.join(folder, "therapist-view.csv"))
    for f, v in (("session.json", sess), ("outcome.json", out), ("summary.csv", reps), ("adaptive-coach.json", coach)):
        if not v and v != []:
            problems.append("missing " + f)
    reps = reps or []
    decisions = coach.get("decisions") or []
    pid = sess.get("participantId") or out.get("participant_id") or "?"
    if sess.get("participantId") and out.get("participant_id") and sess["participantId"] != out["participant_id"]:
        problems.append("participant id differs between session.json and outcome.json")
    dev = bool(sess.get("developerMode")) or any(d.get("developerMode") for d in decisions) \
        or any(r.get("developer_mode") == "1" for r in tv or [])

    # PERFECT per rep: match the therapist view by (exercise, side, n-th occurrence).
    tv_perfect = {}
    if tv is not None:
        seen = defaultdict(int)
        for r in tv:
            k = (r["exercise_id"], r["side"]); seen[k] += 1
            tv_perfect[k + (seen[k],)] = r.get("perfect") == "1"
        if len(tv) != len(reps):
            problems.append(f"therapist-view has {len(tv)} rows, summary.csv {len(reps)}")
    seen = defaultdict(int)
    for r in reps:
        k = (r["exercise_id"], r["side"]); seen[k] += 1
        r["_complete"] = r.get("complete_movement") == "1"
        recomputed = r["_complete"] and num(r.get("score")) >= 90 and num(r.get("travel_coverage")) >= .9
        r["_perfect"] = tv_perfect.get(k + (seen[k],), recomputed) if tv is not None else recomputed
    if len(decisions) != len(reps):
        problems.append(f"{len(decisions)} coach decisions vs {len(reps)} reps")

    active = num(out.get("active_seconds"))
    stamp = name[:15]
    when = f"{stamp[0:4]}-{stamp[4:6]}-{stamp[6:8]} {stamp[9:11]}:{stamp[11:13]}" if len(stamp) == 15 else name
    n = len(reps)
    row = {
        "session": name, "participant": pid, "datetime_utc": when, "outcome": out.get("outcome", "?"),
        "active_min": round(active / 60, 2), "reps": n,
        "complete_pct": round(100 * sum(r["_complete"] for r in reps) / n, 1) if n else "",
        "perfect_pct": round(100 * sum(r["_perfect"] for r in reps) / n, 1) if n else "",
        "perfect_source": "therapist-view" if tv is not None else "recomputed",
        "mean_score": round(sum(num(r["score"]) for r in reps) / n, 1) if n else "",
        "breaks": out.get("breaks", ""), "off_spot_s": round(num(out.get("off_spot_seconds", "")), 1) if "off_spot_seconds" in out else "n/a",
        "tracking_pause_s": round(num(out.get("tracking_pause_seconds")), 1),
        "dev_mode": int(dev), "short": int(active < MIN_ACTIVE_S),
        "included": int(not dev and active >= MIN_ACTIVE_S), "problems": "; ".join(problems),
    }
    return row, reps, decisions


def cell_rows(sessions):
    """sessions: list of (row, reps, decisions) for included sessions."""
    cells = defaultdict(lambda: {"reps": [], "dec": []})
    for row, reps, decisions in sessions:
        key = (row["participant"], row["session"])
        for r in reps:
            cells[key + (r["exercise_id"], r["side"])]["reps"].append(r)
        for d in decisions:
            cells[key + (d.get("exerciseId"), d.get("side"))]["dec"].append(d)
    out, series = [], {}
    for (pid, sid, ex, side), c in sorted(cells.items()):
        reps, dec = c["reps"], c["dec"]
        scores = [num(r["score"]) for r in reps]
        kinds = [kind(d) for d in dec]
        learned = [d for d, k in zip(dec, kinds) if k != "not_learned"]
        tail = learned[len(learned) - -(-len(learned) // 3):]  # last third, rounded up
        def still(d):
            ru, rn = num(d.get("rangeScaleUsed"), 1), num(d.get("nextRangeScale"), 1)
            du, dn = num(d.get("durationScaleUsed"), 1), num(d.get("nextDurationScale"), 1)
            return abs(rn / ru - 1) < RANGE_DZ and abs(du / dn - 1) < SPEED_DZ
        settled = ("yes" if all(still(d) for d in tail) else "no") if len(tail) >= 2 else "too few reps"
        mean = lambda xs: round(sum(xs) / len(xs), 1) if xs else ""
        speed = lambda d, key: round(1 / num(d.get(key), 1), 3)
        out.append({
            "participant": pid, "session": sid, "exercise": ex, "arm": side, "reps": len(reps),
            "complete": sum(r["_complete"] for r in reps), "perfect": sum(r["_perfect"] for r in reps),
            "first3_score": mean(scores[:3]), "last3_score": mean(scores[-3:]),
            "start_range": round(num(dec[0].get("rangeScaleUsed")), 3) if dec else "",
            "final_range": round(num(dec[-1].get("nextRangeScale")), 3) if dec else "",
            "start_speed": speed(dec[0], "durationScaleUsed") if dec else "",
            "final_speed": speed(dec[-1], "nextDurationScale") if dec else "",
            "increases": kinds.count("increase"), "eases": kinds.count("ease"),
            "keeps": kinds.count("keep"), "not_learned": kinds.count("not_learned"), "settled": settled,
            "at_caps_end": int(bool(dec) and num(dec[-1].get("nextRangeScale")) >= .999 and 1 / num(dec[-1].get("nextDurationScale"), 1) >= 1.199),  # speed ceiling 1.2 since 2026-11-18 (1.4 before)
        })
        series[(pid, sid, ex, side)] = ([num(d.get("rangeScaleUsed")) for d in dec], [1 / num(d.get("durationScaleUsed"), 1) for d in dec])
    return out, series


def write_csv(path, rows):
    if not rows:
        return
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader(); w.writerows(rows)


def md_table(rows, cols):
    lines = ["| " + " | ".join(cols) + " |", "|" + "---|" * len(cols)]
    lines += ["| " + " | ".join(str(r[c]) for c in cols) + " |" for r in rows]
    return "\n".join(lines)


def analyse(roots, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    folders, dupes = {}, []
    for root in roots:
        for name in sorted(os.listdir(root)):
            p = os.path.join(root, name)
            if not os.path.isdir(p):
                continue
            if name in folders:  # the same session copied into two export folders
                dupes.append(name); continue
            folders[name] = p
    loaded = [read_session(p) for _, p in sorted(folders.items())]
    srows = [s[0] for s in loaded]
    included = [s for s in loaded if s[0]["included"]]
    crows, series = cell_rows(included)
    write_csv(os.path.join(out_dir, "sessions.csv"), srows)
    write_csv(os.path.join(out_dir, "cells.csv"), crows)

    pids = sorted({r["participant"] for r in srows})
    md = ["# Session analysis", "", f"Folders: {', '.join(roots)}", "",
          f"{len(srows)} sessions, {len(included)} included (not dev mode, >= {MIN_ACTIVE_S} s active). "
          f"Participants: {', '.join(pids)}.", ""]
    if len(pids) == 1 and len(srows) > 1:
        md.append(f"WARNING: every session has participant {pids[0]}; cells.csv pools them as one person.\n")
    if dupes:
        md.append(f"Skipped duplicate session folders: {', '.join(sorted(set(dupes)))}.\n")
    md += ["## Sessions", "", md_table(srows, ["session", "participant", "outcome", "active_min", "reps", "complete_pct",
           "perfect_pct", "mean_score", "breaks", "off_spot_s", "dev_mode", "included", "problems"]), "",
           "## Participant x session x exercise x arm (included sessions)", "",
           md_table(crows, list(crows[0])) if crows else "(none)", "",
           "settled: last third of learned decisions all inside the dead zone (range .02, speed .0225); needs >= 4 learned decisions. Increase/ease/keep count actual level changes. at_caps_end: range 1.0 and speed 1.4 after the last attempt.", ""]
    with open(os.path.join(out_dir, "summary.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(md))

    try:
        import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib not installed: no plots")
    else:
        for (pid, sid, ex, side), (rng, spd) in series.items():
            fig, ax = plt.subplots(figsize=(5, 3))
            ax.plot(range(1, len(rng) + 1), rng, label="range"); ax.plot(range(1, len(spd) + 1), spd, label="speed")
            ax.set(title=f"{pid} {sid} {ex} {side}", xlabel="attempt", ylabel="level"); ax.legend()
            fig.tight_layout(); fig.savefig(os.path.join(out_dir, f"{pid}_{sid}_{ex}_{side}.png")); plt.close(fig)
    return srows, crows


def selftest():
    tmp = tempfile.mkdtemp()
    s = os.path.join(tmp, "20261201T100000000Z"); os.makedirs(s)
    json.dump({"participantId": "P009", "developerMode": False}, open(os.path.join(s, "session.json"), "w"))
    json.dump({"participant_id": "P009", "outcome": "victory", "active_seconds": 120, "breaks": 0, "off_spot_seconds": 1.5},
              open(os.path.join(s, "outcome.json"), "w"))
    hdr = "exercise_id,side,repetition,score,complete_movement,travel_coverage\n"
    rows = [(95, 1, 1.0), (80, 1, .95), (92, 0, 1.0), (100, 1, .85), (99, 1, 1.0), (96, 1, 1.0), (97, 1, .99)]
    open(os.path.join(s, "summary.csv"), "w").write(hdr + "".join(f"scaption,left,{i+1},{a},{b},{c}\n" for i, (a, b, c) in enumerate(rows)))
    # range .5 -> .6 -> .7 then flat; speed 1 -> 1.25 then flat; one tracking-loss rep
    dec = [("Network increases next attempt within limits", .5, .6, 1, .8), ("Network increases", .6, .7, .8, .8),
           ("Tracking unreliable; keep difficulty", .7, .7, .8, .8), ("Network eases next attempt", .7, .65, .8, .8),
           ("Maintain difficulty", .65, .65, .8, .8), ("Maintain difficulty", .65, .655, .8, .8), ("Network increases", .655, .655, .8, .8)]
    json.dump({"decisions": [{"exerciseId": "scaption", "side": "left", "reason": r, "rangeScaleUsed": a, "nextRangeScale": b,
               "durationScaleUsed": c, "nextDurationScale": d} for r, a, b, c, d in dec]}, open(os.path.join(s, "adaptive-coach.json"), "w"))
    srows, crows = analyse([tmp], os.path.join(tmp, "out"))
    r = srows[0]
    assert r["included"] == 1 and r["reps"] == 7 and r["perfect_source"] == "recomputed", r
    assert r["perfect_pct"] == round(100 * 4 / 7, 1), r  # reps 1, 5, 6, 7
    c = crows[0]
    assert (c["perfect"], c["complete"], c["increases"], c["eases"], c["keeps"], c["not_learned"]) == (4, 6, 3, 1, 2, 1), c
    assert (c["start_range"], c["final_range"], c["start_speed"], c["final_speed"]) == (.5, .655, 1.0, 1.25), c
    assert c["settled"] == "yes" and c["first3_score"] == 89.0, c
    # therapist view overrides PERFECT; a short session is excluded
    open(os.path.join(s, "therapist-view.csv"), "w").write("exercise_id,side,perfect\n" + "scaption,left,0\n" * 7)
    json.dump({"participant_id": "P009", "outcome": "aborted", "active_seconds": 30}, open(os.path.join(s, "outcome.json"), "w"))
    srows, crows = analyse([tmp], os.path.join(tmp, "out"))
    assert srows[0]["perfect_pct"] == 0 and srows[0]["included"] == 0 and crows == [], srows
    print("selftest OK")


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args or args == ["--selftest"]:
        selftest(); sys.exit(0)
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
    if "--out" in args:
        i = args.index("--out"); out = args[i + 1]; del args[i:i + 2]
    srows, crows = analyse(args, out)
    print(f"{len(srows)} sessions, {sum(r['included'] for r in srows)} included -> {out}")
