find $1/FileProviderExtension.appex -iname '*.dylib' | while read libfile ; do codesign --force --sign - -o runtime  --timestamp "${libfile}" ; done ;
