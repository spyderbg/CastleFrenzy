#!/usr/bin/env python3
"""Play every campaign day and endless segment of the Unity port's Linux player and report the results.

Each day runs in its own player process, started directly in that day (-cf-level/-cf-day). By default the
-cf-autoplay bot defends the castle through the game's touch input; --autokill instead removes walking units
(a quicker load/finish check). Exits non-zero if any day is lost, never finishes, or logs an exception.

    python3 tools/unity/play_all_days.py                 # bot, all 105 days, 6 at a time
    python3 tools/unity/play_all_days.py --levels 3 4 -j 2
    python3 tools/unity/play_all_days.py --autokill --out /tmp/cf-days
"""
import argparse
import concurrent.futures
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LEVELS = os.path.join(ROOT, "Unity/Assets/StreamingAssets/GameData/Levels")
PLAYER = os.path.join(ROOT, "Unity/Builds/Linux/CastleFrenzy.x86_64")


def days_per_level():
    """Day count of each level from levelNN-dayMM.xml; the last level is Endless."""
    counts = {}
    for name in os.listdir(LEVELS):
        m = re.fullmatch(r"level(\d+)-day(\d+)\.xml", name)
        if m:
            counts[int(m.group(1)) - 1] = counts.get(int(m.group(1)) - 1, 0) + 1
    return [counts[i] for i in sorted(counts)]


def run_day(player, out, level, day, endless, autokill):
    work = os.path.join(out, "L%d_D%d" % (level, day))
    os.makedirs(work, exist_ok=True)
    log = os.path.join(work, "log.txt")
    args = [player, "-screen-width", "480", "-screen-height", "320", "-screen-fullscreen", "0",
            "-cf-frames", "40000", "-cf-save-dir", os.path.join(work, "save"), "-logFile", log,
            "-cf-day", str(day), "-cf-skip-cutscene", "-cf-quit-on-result"]
    args += ["-cf-endless"] if endless else ["-cf-level", str(level)]
    args += ["-cf-autokill", "-cf-timescale", "6"] if autokill else ["-cf-autoplay", "-cf-fast"]
    try:
        subprocess.run(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=1200)
    except subprocess.TimeoutExpired:
        pass
    text = open(log, errors="replace").read() if os.path.exists(log) else ""
    m = re.search(r"\[CastleFrenzy\] Result (.*)", text)
    result = m.group(1).strip() if m else "NO RESULT"
    exceptions = text.count("Exception")
    ok = "victory" in result and exceptions == 0
    return level, day, ok, "L%d D%d | %s | exceptions=%d" % (level, day, result, exceptions)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--player", default=PLAYER, help="Linux player (default: Unity/Builds/Linux)")
    ap.add_argument("--levels", type=int, nargs="*", help="0-based levels to run (default: all, Endless last)")
    ap.add_argument("--autokill", action="store_true", help="kill walking units instead of using the bot")
    ap.add_argument("-j", "--jobs", type=int, default=6, help="players running at once")
    ap.add_argument("--out", help="folder for logs and saves (default: a temporary folder)")
    a = ap.parse_args()

    days = days_per_level()
    endless = len(days) - 1
    out = a.out or tempfile.mkdtemp(prefix="cf-days-")
    jobs = [(lvl, d) for lvl, n in enumerate(days) if a.levels is None or lvl in a.levels for d in range(n)]
    print("%d days, logs in %s" % (len(jobs), out), flush=True)

    results = []
    with concurrent.futures.ThreadPoolExecutor(a.jobs) as pool:
        futures = [pool.submit(run_day, a.player, out, lvl, d, lvl == endless, a.autokill) for lvl, d in jobs]
        for f in concurrent.futures.as_completed(futures):
            results.append(f.result())
            print(results[-1][3], flush=True)

    results.sort()
    failed = [r for r in results if not r[2]]
    print("\n%d/%d days won without exceptions" % (len(results) - len(failed), len(results)))
    for r in failed:
        print("FAILED " + r[3])
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
