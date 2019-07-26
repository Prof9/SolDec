# SolDec
SolDec is a decompiler for bytecode functions and scripts used in the Boktai (Bokura no Taiyou) series of games. The decompiled functions are output as (close to) C code. Currently it has only been tested with the first game, Boktai: The Sun is in Your Hand. Only a handful of control structures and keywords (e.g. if-then-else) are supported at the moment.

Usage:
```
SolDec boktai.gba 0xABCDEF
```
Decompiles bytecode in a ROM starting from the specified file offset. The offset must be a hexadecimal number prefixed with 2 characters.

For example, `SolDec boktai-us.gba 0xE58B5E` decompiles the function that calculates dungeon rank.

![Screenshot](https://i.imgur.com/U9YfMwu.png)
