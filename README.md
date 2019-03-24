# C# extension method for fast object cloning.

This is a speed-optimized fork of Alexy Burtsev's deep copier. Depending on your usecase, this will be 2x - 3x faster than the original. It also fixes some bugs which are present in the original code. Compared to the classic binary serialization/deserialization deep clone technique, this version is about seven times faster (the more arrays your objects contain, the bigger the speedup factor).

The speedup is achieved via the following techniques:

- object reflection results are cached
- don't deep copy primitives or immutable structs & classes (e.g. enum and string)
- to improve locality of reference, process the 'fast' dimensions or multidimensional arrays in the __inner__ loops
- use a compiled lamba expression to call MemberwiseClone

## Contributors:
- Alexy Burtsev (original deep copy code)
- Wouter Groeneveld (unit tests & XElement copy)
- Gitkarst (treat enum as immutable)
- Jean-Paul Mikkers (speed optimization)
