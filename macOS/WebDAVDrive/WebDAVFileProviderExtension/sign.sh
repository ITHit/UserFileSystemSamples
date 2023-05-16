find $1/WebDAVFileProviderExtension.appex -iname '*.dylib' | while read libfile ; do codesign --force --sign - -o runtime  --timestamp "${libfile}" ; done ;
