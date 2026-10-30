#!/usr/bin/env bash
# run.sh - danlang's benchmarks: each program here run by each bundle given (a build with lib/ beside it:
# tests/compat/compat.sh bundle), the best of RUNS (3) runs' wall times, in milliseconds (a run's start, about a
# fifth of a second, too), and their total; beside each bundle's after the first, its speed-up against the first's
#   bench/run.sh BUNDLE...
set -u
here=$(cd "$(dirname "$0")" && pwd)
runs=${RUNS:-3}
progs=$(cd "$here" && ls *.dl | sed 's/\.dl$//')
declare -A best
for b in "$@"; do
    for p in $progs; do
        min=
        for ((r = 0; r < runs; r++)); do
            t0=$(date +%s%N)
            (cd "$here" && "$b/danlang" "$p.dl" > /dev/null 2>&1) || { min=FAIL; break; }
            t=$(( ($(date +%s%N) - t0) / 1000000 ))
            if [ -z "$min" ] || [ "$t" -lt "$min" ]; then min=$t; fi
        done
        best["$b|$p"]=$min
    done
done
printf '%-10s' program; for b in "$@"; do printf '%16s' "$(basename "$b")"; done; echo
for p in $progs total; do
    printf '%-10s' "$p"
    first=
    for b in "$@"; do
        if [ "$p" = total ]; then
            v=0; for q in $progs; do x=${best["$b|$q"]}; [ "$x" = FAIL ] && { v=FAIL; break; }; v=$((v + x)); done
        else v=${best["$b|$p"]}; fi
        if [ -z "$first" ]; then first=$v; printf '%16s' "$v"
        elif [ "$v" = FAIL ] || [ "$first" = FAIL ] || [ "$v" -eq 0 ]; then printf '%16s' "$v"
        else printf '%16s' "$v ($(awk -v a="$first" -v b="$v" 'BEGIN { printf "%.2fx", a / b }'))"; fi
    done
    echo
done
