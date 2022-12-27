((lambda (arg) (* arg 2)) 10)

(defun forall (list func)
 (if (null list)
 T
 (and (funcall func (car list))
 (forall (cdr list) func))))

 (defun non-nil (list)
 (if (null list)
 '()
 (cons
 (if (null (car list))
 0
 1)
 (non-nil (cdr list)))))
(defun non-nil (list)
 (mapcar :((elem)
 (if (null elem)
 0
 1))
 list))
