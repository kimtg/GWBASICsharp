10 REM ==========================================
20 REM  MANDELBROT SET IN TEXT MODE (GW-BASIC)
30 REM ==========================================
40 CLS
50 FOR Y = -12 TO 12
60   FOR X = -39 TO 39
70     CR = X * 0.045
80     CI = Y * 0.085
90     ZR = 0: ZI = 0: ITER = 0
100    MAXITER = 30
110    WHILE (ZR * ZR + ZI * ZI <= 4) AND (ITER < MAXITER)
120      TEMP = ZR * ZR - ZI * ZI + CR
130      ZI = 2 * ZR * ZI + CI
140      ZR = TEMP
150      ITER = ITER + 1
160    WEND
170    IF ITER = MAXITER THEN PRINT "*"; ELSE PRINT " ";
180  NEXT X
190  PRINT
200 NEXT Y
210 PRINT "Mandelbrot generation complete."
