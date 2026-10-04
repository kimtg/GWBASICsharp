10 REM ==========================================
20 REM  PRIME NUMBERS (SIEVE OF ERATOSTHENES)
30 REM ==========================================
40 PRINT "Sieve of Eratosthenes benchmark:"
50 N = 100
60 DIM P(100)
70 FOR I = 2 TO N: P(I) = 1: NEXT I
80 FOR I = 2 TO SQR(N)
90   IF P(I) = 0 THEN GOTO 120
100  FOR J = I * I TO N STEP I: P(J) = 0: NEXT J
120 NEXT I
130 COUNT = 0
140 FOR I = 2 TO N
150  IF P(I) = 1 THEN PRINT I; : COUNT = COUNT + 1
160 NEXT I
170 PRINT
180 PRINT USING "Found ### prime numbers up to 100."; COUNT
