; hardware.inc - Common GameBoy hardware definitions and macros
; This file demonstrates macros in an external include file

; Hardware register addresses
DEF rLCDC EQU $FF40
DEF rSTAT EQU $FF41
DEF rSCY  EQU $FF42
DEF rSCX  EQU $FF43
DEF rLY   EQU $FF44

; ============================================================================
; HARDWARE MACROS
; ============================================================================

; Wait for VBlank (external version)
WaitForVBlank: MACRO
    ld a, [rLY]            ; 2 bytes, 12 cycles
    cp 144                 ; 2 bytes, 8 cycles
    jr c, @-4              ; 2 bytes, 12/8 cycles
ENDM

; Turn off the LCD safely
LCDOff: MACRO
    ld a, [rLCDC]          ; 2 bytes, 12 cycles
    and %01111111          ; 2 bytes, 8 cycles
    ld [rLCDC], a          ; 2 bytes, 12 cycles
ENDM

; Turn on the LCD
LCDOn: MACRO
    ld a, [rLCDC]          ; 2 bytes, 12 cycles
    or %10000000           ; 2 bytes, 8 cycles
    ld [rLCDC], a          ; 2 bytes, 12 cycles
ENDM

; Set scroll position
SetScroll: MACRO
    ld a, \1               ; 2 bytes, 8 cycles - X position
    ld [rSCX], a           ; 2 bytes, 12 cycles
    ld a, \2               ; 2 bytes, 8 cycles - Y position
    ld [rSCY], a           ; 2 bytes, 12 cycles
ENDM

; Copy bytes from source to destination
; Usage: MemCopy source, dest, length
MemCopy: MACRO
    ld hl, \1              ; 3 bytes, 12 cycles - source
    ld de, \2              ; 3 bytes, 12 cycles - dest
    ld bc, \3              ; 3 bytes, 12 cycles - length
.loop\@
    ld a, [hl+]            ; 1 byte, 8 cycles
    ld [de], a             ; 1 byte, 8 cycles
    inc de                 ; 1 byte, 8 cycles
    dec bc                 ; 1 byte, 8 cycles
    ld a, b                ; 1 byte, 4 cycles
    or c                   ; 1 byte, 4 cycles
    jr nz, .loop\@         ; 2 bytes, 12/8 cycles
ENDM
