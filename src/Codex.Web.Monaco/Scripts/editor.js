/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="rpc.ts"/>
var editor;
var codexWebRootPrefix = "";
var currentTextModel;
var sourceFileModel;
var registered;
function registerProviders() {
    if (registered) {
        return;
    }
    registered = true;
    monaco.languages.register({ id: 'csharp' });
    monaco.languages.registerDefinitionProvider('csharp', {
        provideDefinition: function (model, position) {
            var word = model.getWordAtPosition(position);
            return { uri: monaco.Uri.parse("bar/b"), range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } };
            //if (word && word.word === "B") {
            //    
            //}
            //return null;
        }
    });
    monaco.languages.registerReferenceProvider('csharp', {
        provideReferences: function (model, position) {
            var word = model.getWordAtPosition(position);
            if (word && word.word === "B") {
                return [{ uri: monaco.Uri.parse("bar/c"), range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } }];
            }
            return [];
        }
    });
    //monaco.languages.register({ id: 'csharp' });
    //monaco.languages.registerDefinitionProvider('csharp', {
    //    provideDefinition: function (model, position) {
    //        var offset = model.getOffsetAt(position);
    //       var url = codexWebRootPrefix + "/definitionAtPosition/" + encodeURI(project) + "/?filename=" + encodeURIComponent(file) + "&position=" + encodeURIComponent(offset);
    //       return callServer(url, function (data) {
    //           var key = project + "/" + file;
    //           //var uri = monaco.Uri.parse(codexWebRootPrefix + data.url + "42");
    //           var uri = monaco.Uri.parse(key);
    //           return { uri: uri, range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } };
    //       });
    //   } 
    //});
    //monaco.languages.registerReferenceProvider('csharp', {
    //    provideDefinition: function (model, position) {
    //        var offset = model.getOffsetAt(position);
    //        var url = codexWebRootPrefix + "/definitionAtPosition/" + encodeURI(project) + "/?filename=" + encodeURIComponent(file) + "&position=" + encodeURIComponent(offset);
    //        return callServer(url, function (data) {
    //            var key = project + "/" + file;
    //            //var uri = monaco.Uri.parse(codexWebRootPrefix + data.url + "42");
    //            var uri = monaco.Uri.parse(key);
    //            return { uri: uri, range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } };
    //        });
    //    }
    //});
}
//function openEditor(input) {
//    alert(input.resource);
//    var uri = input.resource;
//    return callServer(uri, function(data) {
//        editor.setValue(data.contents);
//    });
//}
function openEditor(input) {
    alert(input.resource);
    var model = models[input.resource];
    if (model) {
        editor.setModel(model);
    }
    return monaco.Promise.as(null);
}
var models;
//var models = {
//};
function createModelFrom(content, project, file) {
    if (currentTextModel) {
        currentTextModel.dispose();
    }
    var key = project + "/" + file;
    return monaco.editor.createModel(content, 'csharp', monaco.Uri.parse(key));
}
function createMonacoEditorAndDisplayFileContent(project, file, sourceFile) {
    editor = undefined;
    sourceFileModel = sourceFile;
    if (!editor) {
        require.config({ paths: { 'vs': 'node_modules/monaco-editor/dev/vs' } });
        var editorPane = document.getElementById('editorPane');
        if (editorPane) {
            require(['vs/editor/editor.main'], function () {
                registerProviders();
                currentTextModel = createModelFrom(sourceFileModel.contents, project, file);
                editor = monaco.editor.create(editorPane, {
                    // Don't need to specify a language, because model carries this information around.
                    model: currentTextModel,
                    readOnly: true,
                    lineNumbers: "on",
                    scrollBeyondLastLine: true
                }, {
                    editorService: { openEditor: openEditor },
                });
                editor.focus();
                if (sourceFile.span) {
                    var monacoPosition = currentTextModel.getPositionAt(sourceFile.span.position);
                    var position = { lineNumber: monacoPosition.lineNumber, column: monacoPosition.column };
                    editor.revealPositionInCenter(position);
                    editor.setPosition(position);
                    editor.deltaDecorations([], [
                        {
                            range: new monaco.Range(position.lineNumber, 1, position.lineNumber, 1),
                            options: { className: 'highlightLine', isWholeLine: true }
                        }
                    ]);
                    editor.setSelection({ startLineNumber: position.lineNumber, startColumn: position.column, endLineNumber: position.lineNumber, endColumn: position.column + sourceFile.span.length });
                }
            });
        }
    }
}
