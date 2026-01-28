# Credits and license

## Credits

**Slash:** Updater code, background image¹, icons² and animations.

1. Background image adapted from my
   ["widescreen levelshots"](https://jkhub.org/files/file/4179-widescreen-levelshots/) pack  
   → Recreation of the original `mp/ffa4` levelshot

2. "Jedi Knight" logo loosely based on:  
   https://en.wikipedia.org/wiki/File:Star_Wars_Jedi_Knight_II_Jedi_Outcast_Logo.svg  
   by Wikipedia user "Maximator2000" (CC0 license).
   
**TaystJK Updater** also includes the [LightJson](https://github.com/MarcosLopezC/LightJson)
library (MIT-licensed), from Marcos López C. ([Why?](technical-info.md#why-lightjson))

Finally, I'd like to mention the following StackOverflow answers:
- [C# - Progress bar with HttpClient](https://stackoverflow.com/a/46497896)
  - [Bruno Zell](https://stackoverflow.com/users/5185376/bruno-zell) and other StackOverflow contributors.
- [What is a good pattern for using a Global Mutex in C#?](https://stackoverflow.com/a/229567)
  - [Sam Saffron](https://stackoverflow.com/users/17174/sam-saffron) and other StackOverflow contributors.
- [Embed git commit hash in a .NET dll](https://stackoverflow.com/a/15145121)
  - [John Jesus](https://stackoverflow.com/users/1761845/john-jesus) and other StackOverflow contributors.


## License 

**TaystJK Updater** is *tentatively* released under the terms of the [**GPL-3.0-or-later**](../LICENSE.txt) license.  
(GNU General Public License v3.0, or any later version).

---

For the sake of completeness, please note that this project uses some code snippets from StackOverflow answers,
with the most substantial portions being in the following two files:
- [HttpClientExtensions.cs](../src/Extensions/HttpClientExtensions.cs)
- [StreamExtensions.cs](../src/Extensions/StreamExtensions.cs)

The above feature amounts to maybe 30 or 40 significant lines of code.

It is my understanding that all of the answers I've benefited from were governed by the **CC BY-SA 4.0** license
at the time of reading, thanks to edits made after May 2, 2018.

https://meta.stackexchange.com/questions/347758/creative-commons-licensing-ui-and-data-updates
> - Content contributed before 2011-04-08 (UTC) is distributed under the terms of
>   [CC BY-SA 2.5](https://creativecommons.org/licenses/by-sa/2.5/).
> - Content contributed from 2011-04-08 up to but not including 2018-05-02 (UTC) is distributed under the terms of
>   [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/).
> - Content contributed on or after 2018-05-02 (UTC) is distributed under the terms of
>   [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
> 
> \[...\]
>
> Every time a piece of content on the site is edited by a user, the editor
> creates a new version of the previous version. The new version of that work is
> considered to be republished and therefore governed under the CC BY-SA version
> in effect as of the day on which the edit was made.

Fortunately, **CC BY-SA 4.0** appears to be
[one-way compatible with GPLv3](https://creativecommons.org/2015/10/08/cc-by-sa-4-0-now-one-way-compatible-with-gplv3/).

> **Obligatory disclaimer:** I am not a lawyer. This is not legal advice. And so on and so forth.

In any case, I do *not* thank StackOverflow for choosing a license that is
[not recommended for source code](https://creativecommons.org/faq/#can-i-apply-a-creative-commons-license-to-software)...
