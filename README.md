# VSDoxyHighlighter <!-- omit in toc -->

![test and build](https://github.com/Sedeniono/VSDoxyHighlighter/actions/workflows/build_and_test.yml/badge.svg)


- [Introduction](#introduction)
- [Installation](#installation)
- [Features](#features)
  - [Syntax highlighting](#syntax-highlighting)
  - [IntelliSense (autocomplete while typing)](#intellisense-autocomplete-while-typing)
  - [Not yet supported and future ideas](#not-yet-supported-and-future-ideas)
- [Configuration](#configuration)
  - [Fonts and colors](#fonts-and-colors)
  - [Comment types](#comment-types)
- [Known problems](#known-problems)



# Introduction

VSDoxyHighlighter is an extension for Visual Studio 2022 to provide **syntax highlighting** and **IntelliSense** (autocomplete while typing) for [Doxygen](https://www.doxygen.nl/index.html) style comments in C/C++.  
Note that Visual Studio Code is **not** supported.



# Installation

Only Visual Studio 2022 is supported.

You can get the extension from the [Visual Studio marketplace](https://marketplace.visualstudio.com/items?itemName=Sedenion.VSDoxyHighlighter).
All releases can also be found [here on github](https://github.com/Sedeniono/VSDoxyHighlighter/releases): Download the `*.vsix` file and open it to install the extension.

For a list of the most important changes in each version (change log), also see the [list of releases](https://github.com/Sedeniono/VSDoxyHighlighter/releases).


# Features
For an introduction of Doxygen, please see [its webpage](https://www.doxygen.nl/index.html).


## Syntax highlighting

The following two images show the default colors used for light and dark color themes by the extension (they are configurable!):
Light             |  Dark 
:--------:|:--------:
![Example dark](Pictures/ExampleLight.png) | ![Example dark](Pictures/ExampleDark.png)

To contrast it, here is the default display without this extension:
Light             |  Dark 
:--------:|:--------:
![Example dark without highlighting](Pictures/ExampleLight_NoHighlight.png) | ![Example dark without highlighting](Pictures/ExampleDark_NoHighlight.png)

The colors aim to make reading and writing Doxygen style comments in Visual Studio easier.
The highlighting effectively performs a rough check while writing them whether the commands are correct.
But even if Doxygen is not used to generate documentation for the source code, I personally have found it convenient to use the commands in order to provide some consistent structure to the documentation.
Especially, important messages such as warnings or notes are harder to overlook while reading the source code.


- The syntax highlighting can be enabled in C/C++ comments starting with `/*`, `/**`, `/*!`, `//`, `///` or `//!`. The enabled comment types can be configured in the Visual Studio options under the VSDoxyHighlighter node. By default, syntax highlighting is not applied to `//` and `/*` comments because Doxygen does not parse those.
- Just like Doxygen, the Javadoc style (commands prefixed by `@` instead of `\`) is also supported. For example, both `\brief` and `@brief` are highlighted.
- [All commands](https://www.doxygen.nl/manual/commands.html) of Doxygen (<= 1.9.5) are supported.
- Highlighting partially supports markdown: **`**bold**`**, __`__bold__`__, *`*italic*`*, _`_italic_`_, ~~`~~strikethrough~~`~~ (tildes), as well as `` `inline code` `` (single backticks only).
- The extension comes with two different default color schemes, one for dark and one for light Visual Studio themes. See the configuration section below for more information.
- Syntax highlighting can be disabled entirely in the VSDoxyHighlighter options.


## IntelliSense (autocomplete while typing)
![Example IntelliSense](Pictures/ExampleIntelliSense.gif)

- If you type an `@` or `\` in a comment, an autocomplete box listing all Doxygen commands appears. Pressing tab, enter or space will autocomplete the currently selected command.
- Just as for the syntax highlighting, the autocomplete box will appear only in comment types (`/*`, `/**`, etc.) which have been enabled. This can be configured in the VSDoxyHighlighter options.
- The box shows the commands as documented on the [Doxygen help page](https://www.doxygen.nl/manual/commands.html). The help text for each command is also taken from there.
- IntelliSense can be disabled entirely in the VSDoxyHighlighter options.



## Not yet supported and future ideas
- Special highlighting of text in "environments" such as `\code` or `\f$`. Note that rendering of latex formulas is not planned, especially since there are already extensions available (e.g. ["TeX Comments"](https://marketplace.visualstudio.com/items?itemName=vs-publisher-1305558.VsTeXCommentsExtension2022) or ["InteractiveComments"](https://marketplace.visualstudio.com/items?itemName=ArchitectSoft.InteractiveCommentsVS2022)).
- Support for [HTML commands](https://www.doxygen.nl/manual/htmlcmds.html) is missing.
- Support for [XML commands](https://www.doxygen.nl/manual/xmlcmds.html) is missing.
- More [markdown support](https://www.doxygen.nl/manual/markdown.html).
- Generating a whole comment block automatically is currently not planned: 
  - Visual Studio 16.6 and above support this out-of-the-box, compare [this blog post](https://devblogs.microsoft.com/cppblog/doxygen-and-xml-doc-comment-support/).
  - There are also extensions available that allow a more fine grained control over the generated comment, e.g. ["Doxygen Comments"](https://marketplace.visualstudio.com/items?itemName=FinnGegenmantel.doxygenComments) or ["DoxygenComments"](https://marketplace.visualstudio.com/items?itemName=NickKhrapov.DoxygenComments2022) (yes, these two extensions have almost the same name).
- Show a help text while hovering over Doxygen commands.



# Configuration

## Fonts and colors
The extension comes with two different color schemes, one for dark and one for light Visual Studio themes.
The appropriate default scheme is selected automatically.
To this end, the detection of the active Visual Studio theme is not coupled to the name of the theme. Instead, the decision is made based on the color of the background. As such, the default colors should be reasonable for more than just the default themes shipped with Visual Studio.

The colors and fonts used for the various keywords can be configured in the Visual Studio settings &rarr; Environment &rarr; Fonts and Colors. All elements corresponding to the extension start with **"VSDoxyHighlighter"**.
Note that Visual Studio stores the settings per color theme.

One thing that you might realize is that the color of ordinary text in "`///`"-comments might be different to the color in other comments.
This has nothing to do with the extension. Visual Studio classifies "`///`"-comments as "XML Doc Comment" and formats them differently by default.
You can change the color in the "Fonts and Colors" settings.


## Comment types
In the Visual Studio options, see the settings under the "VSDoxyHighlighter" node.

You can configure separately for each comment type (`/*`, `/**`, `/*!`, `//`, `///` or `//!`) whether the extension should perform highlighting and autocomplete.
By default, the comment types `//` and `/*` are disabled because Doxygen does not parse those.


# Known problems
- The extension does not work in VS 2019 or earlier. There is currently no plan to support versions older than VS 2022.
- The extension comes with two different color schemes, namely for dark and light color themes. Changing the Visual Studio theme should immediately adapt the comment colors. However, in rare cases this happens only partially (such as some bold formatting not being applied correctly). To fix this, restart Visual Studio. If the colors are still wrong, please uninstall and reinstall the extension.
