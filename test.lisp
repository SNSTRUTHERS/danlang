
; Tail Recursion
; --------------
; tail recursive functions are recursive functions in which
; the recursive call takes place as the very last action before
; returning, e.g.

   ; eventually calculates y * 2^x
   (defun f (x y)
       (cond
           ; if x < 1 return y
           ((< x 1) y)
           ; otherwise compute f(x-1, 2*y)
           (t (f (- x 1) (+ y y)))))

; Note the only recursive call is the last action in the cond,
;    and the cond returns whatever the recursive call returns.

; The significance of tail recursion is that the tail recursive
;     calls can be optimized by the compiler to reuse the stack
;     space from the previous call, which eliminates the extra
;     storage overhead usually forced by recursion and also
;     eliminates the time overhead associated with recursive
;     function calls -- making the recursive function as efficient
;     as traditional loops!
;
; Note that a generalization of this form of optimization is called
;     tail call optimization, applicable whenever the value returned
;     by a function is the direct result of another function call.
;
; gcl does recognize and optimize tail recursion when the code is compiled,
;     but does not tackle the more general case of tail call optimization.

; Most of our recursive examples have been shown in an intuitive form,
;     which often is not tail recursive.  The examples below show versions
;     which are tail recursive for append, reverse, length, min, and count.
;
; For most of these examples, there will be a simple public call that does
;     some error checking and then calls a customized version (usually with
;     extra parameters) to carry out the real work/

; --- Example 1: tail recursive append -----------------------------------------

; (append L1 L2)
; --------------
; return a list whose contents are the elements of list L1
;    followed by the elements of list L2,
; return nil if either parameter is not a list
(defun append (L1 L2)
    (cond
        ; error check
        ((not (listp L1)) nil)
        ((not (listp L2)) nil)
        ; easy case where L2 is empty
        ((null L2) L1)
        ; general case, solve using appendReverse (see below)
        (t (appendReverse (reverse L1) L2))))

; (appendReverse L1 L2)
; ---------------------
; appends L2 to the reverse of L1, assumes both are lists
(defun appendReverse (L1 L2)
    (cond
        ; if L1 is empty we are finished
        ((null L1) L2)
        ; otherwise the first element of L1 should be placed at the front of L2,
        ;    then call recursively to reverse/add the remaining elements of L1
        ;    to the front of the revised L2
        (t (appendReverse (cdr L1) (cons (car L1) L2)))))

; --- Example 2: tail recursive reverse ----------------------------------------

; (reverse L)
; -----------
; return the reverse of L, or nil if L is not a list
(defun reverse (L)
   (cond
       ; make sure L is a list
       ((not (listp L)) nil)
       ; otherwise compute the reverse using (reverseApp L '()),
       ; i.e. prepend the reverse of L to the empty list,
       ;      thus giving the reverse of L
       (t (reverseApp L '()))))

; (reverseApp L soFar)
; --------------------
; prepends the reverse of L to soFar,
;    e.g. if L is '(1 2 3) and soFar is '(4 5 6) this returns (3 2 1 4 5 6)
; assumes L and soFar are both lists
(defun reverseApp (L soFar)
   (cond
       ; if L is empty we're done, just return soFar
       ((null L) soFar)
       ; otherwise move the front of L to the front of soFar,
       ;    then call recursively to process the rest of L
       (t (reverseApp (cdr L) (cons (car L) soFar)))))


; --- Example 3: tail recursive length -----------------------------------------

; (length L)
; ----------
; return a count of the number of elements in list L
; returns nil if L is not a list
(defun length (L)
   (cond
       ; make sure L is a list
       ((not (listp L)) nil)
       ; otherwise use lengthAcc to compute the cumulative length of L,
       ;    starting with a length of 0
       (t (lengthAcc L 0))))

; (lengthAcc L soFar)
; -------------------
; computes and returns the sum of soFar and the length of list L
; assumes L is a list and soFar is an integer
(defun lengthAcc (L soFar)
   (cond
       ; if L is empty the sum is 0+soFar, i.e. soFar
       ((null L) soFar)
       ; otherwise add one to soFar and recurse on the tail of L
       (t (lengthAcc (cdr L) (+ 1 soFar)))))


; --- Example 4: tail recursive min --------------------------------------------

; (min L)
; -------
; assuming L is a list of numbers, min returns the smallest number in L
; returns nil if L is empty or contains any non-numeric entries
(defun min (L)
   (cond
       ; make sure L is a non-empty list
       ((not (listp L)) nil)
       ((null L) nil)
       ; make sure L begins with a number
       ((not (numberp (car L))) nil)
       ; use minSoFar, where the head of L is the initial min
       ;     and the tail of L is the set of remaining elements to check
       (t (minSoFar (cdr L) (car L)))))

; (minSoFar L prevMin)
; --------------------
; assuming L is a list of numbers and prevMin is a number,
;    return the smallest of prevMin and the numbers in L
(defun minSoFar (L prevMin)
   (cond
       ; if L is empty then prevMin is the smallest
       ((null L) prevMin)
       ; if the head of L isn't a number then return nil (error)
       ((not (numberp (car L))) nil)
       ; if prevMin is smaller than the head of L
       ;    then call recursively on the tail of L and prevMin
       ((< prevMin (car L)) (minSoFar (cdr L) prevMin))
       ; otherwise call recursively on the tail of L
       ;    but using the head of L as the new minimum so far
       (t (minSoFar (cdr L) (car L)))))

; --- Example 5: tail recursive fibonacci --------------------------------------

; (fib N)
; -------
; returns the Nth fibonacci number,
; where fib(1) is 1, fib(2) is 1, fib(3) is 2, etc
; returns nil if N is non-integer, 0 if N is < 1
(defun fib (N)
   (cond
      ((not (integerp N)) nil)
      ((< N 3) 1)
      ; use the tail recursive fibAcc with accumulators
      (t (fibAcc N 3 1 1))))

(defun fibAcc (N NewN prevN lastN)
   (cond
      ((< N NewN) lastN)
      (t (fibAcc N (+ NewN 1) lastN (+ prevN lastN)))))
