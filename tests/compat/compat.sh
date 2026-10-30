#!/usr/bin/env bash
# compat.sh - danlang's compatibility check: programs whose whole output (stdout, stderr and the exit status) must
# stay the same from one build of danlang to the next.  A case is tests/compat/cases/<area>/<name>.dl, run as a
# program (danlang name.dl, in a folder of its own, so its traces say name.dl), or <name>.repl, given to the REPL as
# its input; a first line "; flags: -w" gives danlang those flags.  Its output is kept beside it, as <name>.out.
#
#   compat.sh bundle DIR               publish Release to DIR, with lib/ beside it: a bundle (its danlang
#                                      loads that lib/globals.dl, from wherever it's run)
#   compat.sh expect BUNDLE [CASE...]  each case's output from BUNDLE, kept as its .out
#   compat.sh check BUNDLE [CASE...]   each case's output from BUNDLE against its .out: the differences, and a count
#   compat.sh diff A B [CASE...]       each case's output from bundles A and B, compared (no .out needed)
#   compat.sh show BUNDLE CASE         a case's output from BUNDLE
#
# A CASE is a file, or a folder (its cases, all the way down); none: all of tests/compat/cases.  The status is 1 if a
# case differs.  COMPAT_JOBS: how many cases run at once (8); TMPDIR: where their folders are made.
set -u
here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
jobs=${COMPAT_JOBS:-8}
tmpbase=${TMPDIR:-/tmp}
mkdir -p "$tmpbase"

# The cases named (files, or the cases in folders), or all of them
cases() {
    if [ $# -eq 0 ]; then set -- "$here/cases"; fi
    for c in "$@"; do
        if [ -d "$c" ]; then find "$c" -type f \( -name '*.dl' -o -name '*.repl' \) | sort
        else echo "$c"; fi
    done
}

# Text with a path in it (as Windows writes it, and with forward slashes) made a name
unpath() {
    local text=$1 path=$2 name=$3 win fwd
    win=$(cygpath -w "$path" 2>/dev/null || echo "$path")
    fwd=${win//\\//}
    text=${text//"$win"/$name}
    text=${text//"$fwd"/$name}
    printf '%s' "$text"
}

# A case's output from a bundle: stdout, stderr and the exit status (the bundle's path made <BUNDLE>, the case's
# folder <CWD>)
run_case() {
    local bundle case=$2 dir name flags out err
    bundle=$(cd "$1" && pwd)
    dir=$(mktemp -d "$tmpbase/compat.XXXXXX")
    name=$(basename "$case")
    cp "$case" "$dir/"
    flags=$(head -1 "$case" | tr -d '\r' | sed -n 's/^; *flags: *//p')
    if [[ $name == *.repl ]]; then
        (cd "$dir" && timeout 120 "$bundle/danlang" $flags < "$name" > out.txt 2> err.txt; echo $? > code.txt)
    else
        (cd "$dir" && timeout 120 "$bundle/danlang" $flags "$name" < /dev/null > out.txt 2> err.txt; echo $? > code.txt)
    fi
    out=$(cat "$dir/out.txt"; echo x); out=${out%x}
    err=$(cat "$dir/err.txt"; echo x); err=${err%x}
    out=$(unpath "$out" "$bundle" "<BUNDLE>"); out=$(unpath "$out" "$dir" "<CWD>"; echo x); out=${out%x}
    err=$(unpath "$err" "$bundle" "<BUNDLE>"); err=$(unpath "$err" "$dir" "<CWD>"; echo x); err=${err%x}
    printf -- '--- stdout\n%s\n--- stderr\n%s\n--- exit %s\n' "$out" "$err" "$(cat "$dir/code.txt")"
    rm -rf "$dir"
}

# One case, for the jobs: MODE A B CASE RESULTS (a line in RESULTS/<key>.status, and what differs in <key>.diff)
one() {
    local mode=$1 a=$2 b=$3 case=$4 res=$5 key want got
    key=$(printf '%s' "$case" | md5sum | cut -c1-16)
    want="${case%.*}.out"
    case $mode in
        expect)
            run_case "$a" "$case" > "$want"
            echo "ok $case" > "$res/$key.status" ;;
        check)
            if [ ! -f "$want" ]; then echo "missing $case" > "$res/$key.status"; return; fi
            run_case "$a" "$case" > "$res/$key.got"
            if cmp -s "$want" "$res/$key.got"; then echo "ok $case" > "$res/$key.status"
            else
                echo "DIFF $case" > "$res/$key.status"
                diff -u --label "expected ${case#$root/}" --label "got" "$want" "$res/$key.got" | head -60 > "$res/$key.diff"
            fi ;;
        diff)
            run_case "$a" "$case" > "$res/$key.a"
            run_case "$b" "$case" > "$res/$key.b"
            if cmp -s "$res/$key.a" "$res/$key.b"; then echo "ok $case" > "$res/$key.status"
            else
                echo "DIFF $case" > "$res/$key.status"
                diff -u --label "A ${case#$root/}" --label "B" "$res/$key.a" "$res/$key.b" | head -60 > "$res/$key.diff"
            fi ;;
    esac
}

# The cases run, so many at once, and what they found: the differences, then the count
run_all() {
    local mode=$1 a=$2 b=$3; shift 3
    local res; res=$(mktemp -d "$tmpbase/compat-results.XXXXXX")
    cases "$@" | tr '\n' '\0' | xargs -0 -P "$jobs" -I{} "$0" _one "$mode" "$a" "$b" {} "$res"
    local total ok bad missing
    total=$(cat "$res"/*.status 2>/dev/null | wc -l)
    ok=$(cat "$res"/*.status 2>/dev/null | grep -c '^ok ')
    missing=$(cat "$res"/*.status 2>/dev/null | grep '^missing ' | sed 's/^missing //')
    bad=$(cat "$res"/*.status 2>/dev/null | grep -c '^DIFF ')
    for d in "$res"/*.diff; do [ -f "$d" ] && { cat "$d"; echo; }; done
    [ -n "$missing" ] && { echo "no .out (run expect first):"; echo "$missing"; }
    echo "$total cases: $ok the same, $bad different${missing:+, some without .out}"
    rm -rf "$res"
    [ "$bad" -eq 0 ] && [ -z "$missing" ]
}

cmd=${1:-}; shift || true
case $cmd in
    bundle)
        dest=$1
        (cd "$root" && dotnet publish -c Release -nologo -v q -o "$dest" 2>&1 | grep -E " error |warning" ; true)
        mkdir -p "$dest/lib"
        cp "$root"/lib/*.dl "$dest/lib/"
        echo "bundle: $dest" ;;
    expect) a=$1; shift; run_all expect "$a" - "$@" ;;
    check)  a=$1; shift; run_all check "$a" - "$@" ;;
    diff)   a=$1; b=$2; shift 2; run_all diff "$a" "$b" "$@" ;;
    show)   run_case "$1" "$2" ;;
    _one)   one "$@" ;;
    *)      sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
