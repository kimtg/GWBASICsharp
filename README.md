# GWBASICsharp

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build & Tests](https://img.shields.io/badge/Tests-41%20Passed-brightgreen.svg)]()

An authentic, modern C# (.NET 10) implementation of **Microsoft GW-BASIC 3.23** (1983–1988), faithfully reproducing classic IBM PC BASIC syntax, runtime semantics, quirks, sound synthesis, CGA graphics, and direct-mode interactive developer experience.

---

## Features & GW-BASIC Fidelity

### 1. Unified Console & On-Demand Graphics
- **Terminal Direct Mode & REPL**:
  - Classic 80x25 terminal layout with alternate screen buffer support.
  - Authentic 25th row function key soft labels (`1LIST 2RUN<- 3LOAD" 4SAVE" 5CONT 6,"LPT1 7TRON 8TROFF 9KEY 0SCREEN`).
  - Full in-place line editing with history, cursor motion, and live function key macro expansion (F1–F10).
  - Just run `dotnet run` directly from the repository root!
- **On-Demand CGA Graphics Window**:
  - Automatically pops up whenever entering graphics mode via `SCREEN 1` (320x200 CGA 4-color) or `SCREEN 2` (640x200 CGA 2-color high-resolution).
  - Hardware-scaled pixel rendering (640x400 internal canvas scaled to retro CRT aspect ratio) with double-buffering.
  - Pixel-accurate CGA graphics primitives: `LINE` (with Bresenham rasterization, box, and filled box), `CIRCLE` (with aspect ratio), `PAINT` (instant scanline flood fill), `PSET`, `PRESET`, `POINT`, and full `DRAW` macro language.
  - In-window text printing: `PRINT`, `PRINT USING`, and `LOCATE` render directly inside the graphics canvas.
  - Switching back to `SCREEN 0` seamlessly hides the graphics window and returns input focus to the terminal.

### 2. Language Semantics & Syntax
- **Classic Line Numbering & Direct Mode**:
  - Direct execution without line numbers; program storage and editing with line numbers.
  - In-place line insertion, replacement, and deletion (`<line_number>` with empty body).
  - `RENUM [new][,old][,inc]` renumbers program lines and updates all branch targets (`GOTO`, `GOSUB`, `THEN`, `ELSE`, `ON..GOTO`, `RUN`, `RESTORE`).
  - `DELETE [start][-end]` removes line ranges.
- **Data Types & Variables**:
  - Case-insensitive identifiers.
  - Explicit sigils: `%` (16-bit Integer: -32768 to 32767), `!` (Single-precision float), `#` (Double-precision float), `$` (String).
  - Default type ranges via `DEFINT`, `DEFSNG`, `DEFDBL`, `DEFSTR` (e.g. `DEFINT A-Z`).
  - Exact GW-BASIC number printing: positive numbers are padded with a leading space; all numbers have a trailing space.
  - Full numeric operators: `+`, `-`, `*`, `/`, `\` (integer division with banker's rounding), `MOD`, `^` (power).
  - Bitwise / logical operators: `AND`, `OR`, `XOR`, `NOT`, `EQV`, `IMP` (with `-1` representing True).
- **Arrays & Collections**:
  - Dynamic `DIM` with multi-dimensional support (`DIM A(10, 20)`).
  - `OPTION BASE 0` and `OPTION BASE 1`.
  - `SWAP variable, variable` and `SWAP array(i), array(j)`.
- **Control Flow**:
  - `FOR .. TO .. STEP .. NEXT` (supporting loop variables, nested loops, and `NEXT I, J`).
  - `WHILE .. WEND`.
  - `IF .. THEN .. ELSE` (supporting line numbers as implied `GOTO`, nested statements).
  - `GOTO` and `GOSUB .. RETURN [line]`.
  - `ON expr GOTO line1, line2, ...` and `ON expr GOSUB line1, line2, ...`.
  - `STOP` (pauses execution with `Break in line`) and `CONT` (resumes execution).
  - `END` and `SYSTEM` (exits interpreter back to OS shell).
- **Data & Functions**:
  - `DATA`, `READ`, and `RESTORE [line]`.
  - User-defined single-line functions via `DEF FNname(args) = expr`.
- **Error Handling**:
  - `ON ERROR GOTO line`.
  - `RESUME`, `RESUME NEXT`, and `RESUME line`.
  - Error variables `ERR` (error code) and `ERL` (line number where error occurred).
  - Standard error codes: `Syntax error`, `Type mismatch`, `Subscript out of range`, `Division by zero`, `Duplicate Definition`, `Out of DATA`, `Undefined line number`, etc.
- **File I/O**:
  - Sequential files: `OPEN file FOR INPUT|OUTPUT|APPEND AS #num`, `PRINT #num`, `WRITE #num`, `INPUT #num`, `LINE INPUT #num`, `CLOSE [#num]`.
  - Random Access files: `OPEN file AS #num LEN = reclen`, `FIELD #num, width AS var$, ...`, `LSET`, `RSET`, `PUT #num, [rec]`, `GET #num, [rec]`.
  - Binary numeric packing: `MKI$`, `CVI`, `MKS$`, `CVS`, `MKD$`, `CVD`.
  - File status: `EOF(num)`, `LOF(num)`, `LOC(num)`.
  - Disk management: `FILES [pattern]`, `KILL filename`, `NAME old AS new`.
- **Print Formatting (`PRINT USING`)**:
  - Numeric masks: `#` (digit), `.` (decimal), `,` (comma grouping), `+` / `-` (sign), `$$` (floating dollar), `**` (asterisk fill), `**$` (combined), `^^^^` (exponential notation).
  - String masks: `!` (first character), `\   \` (fixed width), `&` (variable length).
  - Literal escaping with `_` (e.g. `_#` prints literal `#`).
- **Graphics Engine**:
  - `SCREEN 0` (Text), `SCREEN 1` (320x200 CGA 4-color), `SCREEN 2` (640x200 CGA 2-color).
  - `COLOR [fg][,[bg][,border]]`, `CLS`, `LOCATE [row][,[col][,cursor]]`.
  - `PSET (x, y)[, c]`, `PRESET (x, y)`.
  - `LINE [(x1, y1)]-(x2, y2)[, [color][, [B|BF]]]`.
  - `CIRCLE (x, y), radius[, [color][, [start][, [end][, aspect]]]]`.
  - `PAINT (x, y)[, [paint_color][, boundary_color]]` (flood fill).
  - `DRAW string`: Complete Graphics Macro Language supporting `U`, `D`, `L`, `R`, `E`, `F`, `G`, `H`, `M[+|-]x,[+|-]y`, `B` (move without plotting), `N` (plot without moving cursor), `C` (color), `S` (scale), `A` (rotation).
- **Audio Synthesis**:
  - `BEEP` (standard 800 Hz alert tone).
  - `SOUND freq, duration`.
  - `PLAY string`: Complete Music Macro Language (MML) supporting notes `A`–`G`, sharps `#`/`+`, flats `-`, octaves `O0`–`O6`, octave shift `>` / `<`, note durations `L1`–`L64`, dotted notes `.`, pauses `P1`–`P64`, tempo `T32`–`T255`, and articulation (`MN` Normal, `ML` Legato, `MS` Staccato).
- **Memory Emulation**:
  - `DEF SEG [= segment]`.
  - `PEEK(address)` and `POKE address, byte`.
- **Program & Environment Commands**:
  - `LIST [start][-end]`, `LLIST`.
  - `LOAD filename[,R]`, `SAVE filename[,A]`, `MERGE filename`.
  - `KEY keyNum, string$` and `KEY ON` / `KEY OFF`.
  - `TRON` and `TROFF` (trace mode displays execution path: `[10][20]...`).

---

## Solution Structure

```
GWBASICsharp/
├── GWBASICsharp.slnx               # Visual Studio / .NET Solution
├── GWBASIC.Console.csproj          # Host Console Application (runs directly with 'dotnet run')
├── Program.cs                      # Entry point, REPL, argument handling & alternate buffer
├── Drivers/                        # Host Drivers
│   ├── ConsoleAudioDriver.cs       # Win32 Console PC Speaker / Beep audio synthesis
│   ├── ConsoleInputDriver.cs       # In-place line editor, key queue & F1-F10 expansion
│   ├── ConsoleScreenDriver.cs      # Screen buffer, CGA rasterizer & window manager
│   └── GraphicsWindow.cs           # On-demand WinForms CGA display window
├── src/
│   └── GWBASIC.Core/               # Core engine (Parser, Lexer, Runtime, Drivers)
│       ├── Common/                 # Values, Types, CGA Palette, Error Codes
│       ├── Lexer/                  # Tokenizer & Keyword Tables
│       ├── Parser/                 # Pratt Expression & Statement AST
│       ├── Runtime/                # Environment, Program, Interpreter, MML, Draw, Formatter
│       └── Drivers/                # IScreenDriver, IAudioDriver, IInputDriver interfaces
├── tests/
│   └── GWBASIC.Tests/              # 41 Unit & Integration Tests (xUnit)
└── samples/                        # Classic GW-BASIC Programs
    ├── ELIZA.BAS                   # Classic Rogerian psychotherapist chatbot
    ├── FILEIO.BAS                  # Random access binary record database
    ├── GRAPHICS.BAS                # CGA Screen 1 palette, lines, circle fill, DRAW
    ├── LUNAR.BAS                   # Apollo 11 Lunar Lander physics simulation
    ├── MANDEL.BAS                  # Mandelbrot fractal set ASCII visualizer
    ├── MUSIC.BAS                   # Beethoven's Ode to Joy using PLAY MML
    └── PRIMES.BAS                  # Sieve of Eratosthenes benchmark
```

---

## Building and Running

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/) or later
- Windows 10/11

### Build the Solution
```powershell
dotnet build GWBASICsharp.slnx
```

### Run Tests
```powershell
dotnet test GWBASICsharp.slnx
```

### Run the Interactive Console REPL
Simply execute `dotnet run` directly from the repository root:
```powershell
dotnet run
```

### Run a Program Directly
```powershell
dotnet run samples/PRIMES.BAS
dotnet run samples/GRAPHICS.BAS
dotnet run samples/MANDEL.BAS
dotnet run samples/MUSIC.BAS
dotnet run samples/FILEIO.BAS
dotnet run samples/LUNAR.BAS
dotnet run samples/ELIZA.BAS
```

---

## Sample Programs

### 1. CGA Graphics Demo (`GRAPHICS.BAS`)
Opens the on-demand graphics window in `SCREEN 1` (320x200 4-color CGA), draws filled color boxes, a circle with flood fill (`PAINT`), and a box using the `DRAW` macro language:
```basic
40 SCREEN 1: CLS: COLOR 1, 0
50 PRINT "SCREEN 1 - 320x200 CGA 4-COLOR MODE"
70 LINE (10, 30)-(100, 70), 1, BF
80 LINE (110, 30)-(200, 70), 2, BF
90 LINE (210, 30)-(300, 70), 3, BF
110 CIRCLE (160, 130), 40, 1
120 PAINT (160, 130), 2, 1
140 DRAW "BM160,130 C3 U20 R20 D20 L20"
150 PRINT "Graphics rendering complete."
```

### 2. Sieve of Eratosthenes (`PRIMES.BAS`)
Calculates and displays primes up to 100 with formatted counts:
```basic
10 REM PRIME NUMBERS (SIEVE OF ERATOSTHENES)
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
```

### 3. Random Access Records (`FILEIO.BAS`)
Demonstrates database records stored in fixed-length files using `FIELD`, `LSET`, `PUT`, `GET`, `MKI$`, and `MKD$`:
```basic
50 OPEN "EMPLOYEES.DAT" AS #1 LEN = 32
60 FIELD #1, 4 AS ID$, 20 AS NAME$, 8 AS SALARY$
80 LSET ID$ = MKI$(101)
90 LSET NAME$ = "ALAN TURING"
100 LSET SALARY$ = MKD$(75000.50)
110 PUT #1, 1
170 CLOSE #1
...
220 GET #1, 1
230 EID% = CVI(ID$): ENAME$ = NAME$: ESAL# = CVD(SALARY$)
260 PRINT USING "RECORD _#: ## | ID: #### | NAME: \                  \ | SALARY: $$##,###.##"; 1; EID%; ENAME$; ESAL#
```

### 4. Beethoven's Ode to Joy (`MUSIC.BAS`)
Plays multi-octave music via the Music Macro Language:
```basic
40 PRINT "Playing Beethoven's Ode to Joy..."
50 PLAY "T140 O3 L4 E E F G G F E D C C D E E. D8 D2"
60 PLAY "E E F G G F E D C C D E D. C8 C2"
70 PLAY "D D E C D F8 E8 C D F8 E8 D C D P4"
80 PLAY "E E F G G F E D C C D E D. C8 C2"
90 PRINT "Song finished!"
```

---

## License

This project is licensed under the MIT License.
