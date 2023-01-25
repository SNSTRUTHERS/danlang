; Special thanks to buildyourownlisp.com for the inspiration from 'lispy'!

(def {pi}            3.14159_26535_89793_23846_26433_83279_50288_41971_69399_37510_58209_74944_59230_78164_06286_20899_86280_34825_34211_70679_82148_08651_32823_06647_09384)
(def {NIL} {})
(def {T} 1)
(def {fun}           (fn {a b} {def (head a) (fn (tail a) b)}))
(fun {cons a b}      {join (list a) b})
; (fun {lambda f}    {fn NIL f})
(fun {local-fun a b} {set (head a) (fn (tail a) b)})

; Comparisons
(fun {>= a b}     {< b a})
(fun {<= a b}     {> b a})
(fun {not a}      {if a {NIL} {T}})
(fun {neq a b}    {not (eq a b)})
(def {==}         eq)
(def {!=}         neq)

; Logical operators
(fun {or a b} {
  if a {T} {
    if b {T} {NIL}
  }
})

(fun {and a b} {
  if a {
    if b {T} {NIL}
  } {NIL}
})

(fun {xor a b} {
  and (or a b) (not (and a b))
})

; Currying
(fun {unpack f l}  {eval (cons f l)})
(fun {pack f}      {f &_})

(def {apply} unpack)
(def {curry} unpack)
(def {uncurry} pack)

; Reverse
(fun {reverse} {if (neq &1 {}) {join (reverse (tail &1)) (head &1)} {NIL}})

; nth element 
(fun {fst l} {eval (head l)})
(fun {snd l} {eval (head (tail l))})
(fun {thd l} {eval (head (tail (tail l)))})

(fun {nth n l} {
  if (== n 0)
    {fst l}
    {nth (- n 1) (tail l)}
})

(fun {len} {if (eq &1 {}) {0} {+ 1 (len (tail &1))}})

(fun {last l} {nth (- (len l) 1) l})

; Take N items
(fun {take n l} {
  if (== n 0)
    {NIL}
    {join (head l) (take (- n 1) (tail l))}
})

; Drop N items
(fun {drop n l} {
  if (== n 0)
    {l}
    {drop (- n 1) (tail l)}
})

; Split at N
(fun {split n l} {list (take n l) (drop n l)})

(fun {elem x l} {
  if l
    {if (== x (fst l)) {T} {elem x (tail l)}}
    {NIL}
})

; create a private scope
(fun {let x} {((fn NIL x) ())})

; do a number of items and return value of the last one
(fun {do} {if (neq (len &_) 0) {last &_} {NIL}})

(def {begin} do)
(def {block} do)

; helpers
(fun {flip f a b} {f b a})
(fun {ghost}      {eval &_})
(fun {comp f g x} {f (g x)}) ; Compose

; map
(fun {map f l} {
  if l
    {cons (f (fst l)) (map f (tail l))}
    {NIL}
})

; filter
(fun {filter f l} {
  if l
    {join (if (f (fst l)) {head l} {NIL}) (filter f (tail l))}
    {NIL}
})

; fold left
(fun {foldl f z l} {
  if l
    {foldl f (f z (fst l)) (tail l)}
    {z}
})

(fun {sum s}     {foldl + 0 s})
(fun {product p} {foldl * 1 p})

; Switch/Cond
(fun {cond} {
  if (eq  &_ {})
    {error "No Selection Found"}
    { if (fst (fst &_))
      {snd (fst &_)}
      {apply cond (tail &_)} }
})

;(def {switch} cond)

(fun {case x} {
  if (eq &_ {})
    {error "No Case Found"}
    {if (eq x (fst (fst &_)))
       {snd (fst &_)}
       {apply case (cons x (tail &_))}}
})

; Fibonacci
(fun {fib n} {
  cond
  { (eq n 0) 0 }
  { (eq n 1) 1 }
  { T (+ (fib (- n 1)) (fib (- n 2))) }
})
