; examples using read


; read any lisp term
(format t "Please enter any valid lisp item:~%")
(setf x (read))
(format t "You entered ~A~%" x)

; read a character
(format t "Please enter a character (e.g. #\x):~%")
(setf x (read-char))
(format t "You entered ~A~%" x)
; now read and discard the newline character
(read-char)

; read a line of text
(format t "Please enter a line of text~%")
(setf x (read-line))
(format t "You entered '~A'~%" x)

; read from a string instead of standard input
(setf str "23 is a number")
(setf x (read-from-string str))
(format t "result of reading first item from '~A' is '~A'~%" str x)

; you can clear (discard) any unread input using
(clear-input)

; (prompt str argList)
; --------------------
; function that takes a format string and list of arguments as parameters,
;    prints the resulting prompt,
; then read and return the user's response
(defun prompt (str argList)
   (eval (append (list 'format t str) argList))
   (read))

; try out our function:
(defvar min 1)
(defvar max 100)
(defvar result (prompt "Enter a value between ~A and ~A~%" '(min max)))
(format t "You chose ~A~%" result)