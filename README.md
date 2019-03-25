# bobbin
<img src=https://raw.githubusercontent.com/radiatoryang/bobbin/master/Media/bobbin_screenshot01.png> 

Bobbin is a small Unity editor tool that automatically downloads files into your Unity project
- downloads files (text, images, etc) from any URL, and/or any publicly viewable Google Docs or Google Sheets without any login or OAuth
- perfect for collaborating with designers, writers, or translators... edit stuff collaboratively in browser, and then auto-import the data directly into your game
    - ___NOTE: it's up to you to write game code to read / process the files!___
    - ___NOTE: this is a one-way no-auth read-only sync, you **CANNOT** use this to upload local changes back into Google Docs, etc.___
- all your settings (URLs and file paths) are saved in a ScriptableObject, convenient for source control
- really lightweight; the core code is just 3 C# files

## how to use it
- **go to ["Releases"](https://github.com/radiatoryang/bobbin/releases), download the latest unitypackage, and import it into your Unity project**
- for more info / help, see the [Wiki](https://github.com/radiatoryang/bobbin/wiki)
- pairs well with [Yarn Spinner](https://github.com/thesecretlab/YarnSpinner)
- tested on Unity 2018.3.2f1

## if you need a serious spreadsheet solution, this isn't it
This is just a simple tool that only downloads files, and does nothing more! For a big fancy tool that can generate data-types / C# auto-completion, here are some better options for integrating spreadsheet data into your game:
- [Meta Sheets](http://metasheets.com/)
- [CastleDB](http://castledb.org/) / [CastleDB-Unity-Importer](https://github.com/kkukshtel/castledb-unity-importer)

## license
- MIT

## acknowledgments
- this uses [Editor Coroutines](https://github.com/marijnz/unity-editor-coroutines)
- this uses [TreeView](https://docs.unity3d.com/Manual/TreeViewAPI.html)
