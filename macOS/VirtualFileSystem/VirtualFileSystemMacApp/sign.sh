find "$1/Virtual Filesystem Sample.app" -iname '*.dylib' | while read libfile ; do codesign --force --sign - -o runtime  --timestamp "${libfile}" ; done ;
