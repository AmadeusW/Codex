/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="rpc.ts"/>

// Required by monaco. Have no idea how to use it without it.
declare const require: any;

declare class SymbolicUri extends monaco.Uri {
    projectId: string;
    symbol: string;
} 

function registerProviders() {
    if (state.editorRegistered) {
        return;
    }

    state.editorRegistered = true;

    monaco.languages.register({ id: 'csharp' });

    monaco.languages.registerDefinitionProvider('csharp', {
        provideDefinition: function (model, position) {
            let offset = model.getOffsetAt(position);
            let definition = getReference(state.sourceFileModel, offset);
            let uri = <SymbolicUri>monaco.Uri.parse(`${encodeURI(definition.projectId)}/${encodeURI(definition.symbol)}`);
            uri.projectId = definition.projectId;
            uri.symbol = definition.symbol;

            return {
                uri: uri,
                range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 }
            }
        }
    });

    monaco.languages.registerReferenceProvider('csharp', {
        provideReferences: function (model, position) {
            var word = model.getWordAtPosition(position);
            if (word && word.word === "B") {
                return [{ uri: monaco.Uri.parse("bar/c"), range: { startLineNumber: 1, startColumn: 7, endLineNumber: 1, endColumn: 8 } }]
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
//        state.editor.setValue(data.contents);
//    });
//}

async function openEditor(input: { resource: SymbolicUri }) {
    let definitionLocation = await getDefinitionLocation(input.resource.projectId, input.resource.symbol);
    if (typeof definitionLocation === "string") {

    } else {
        var model = createModelFrom(
            definitionLocation.contents,
            definitionLocation.projectId,
            definitionLocation.filePath);
        state.sourceFileModel = definitionLocation;

        state.editor.setModel(model);

        // TODO: add try/catch, refactor the logic out
        state.editor.focus();
        if (definitionLocation.span) {
            var monacoPosition = state.currentTextModel.getPositionAt(definitionLocation.span.position);

            var position = { lineNumber: monacoPosition.lineNumber, column: monacoPosition.column };
            state.editor.revealPositionInCenter(position);
            state.editor.setPosition(position);
            state.editor.deltaDecorations([],
                [
                    {
                        range: new monaco.Range(position.lineNumber, 1, position.lineNumber, 1),
                        options: { className: 'highlightLine', isWholeLine: true }
                    }
                ]);
            state.editor.setSelection({ startLineNumber: position.lineNumber, startColumn: position.column, endLineNumber: position.lineNumber, endColumn: position.column + definitionLocation.span.length });
        }                    
    }

    return monaco.Promise.as(null);
}

var models;
//var models = {

//};

function createModelFrom(content: string, project: string, file: string) {
    if (state.currentTextModel) {
        state.currentTextModel.dispose();
    }

    var key = `${project}/${file}`;
    state.currentTextModel = monaco.editor.createModel(content, 'csharp', monaco.Uri.parse(key));
    return state.currentTextModel;
}

function createMonacoEditorAndDisplayFileContent(project: string, file: string, sourceFile: SourceFileContentsModel) {
    state.editor = undefined;
    state.sourceFileModel = sourceFile;
    if (!state.editor) {
        require.config({ paths: { 'vs': 'node_modules/monaco-editor/dev/vs' } });

        var editorPane = document.getElementById('editorPane');
        if (editorPane) {
            require(['vs/editor/editor.main'],
                function () {
                    class SymbolicUri extends monaco.Uri {
                        projectId: string;
                        symbol: string;
                    } 

                    registerProviders();

                    state.currentTextModel = createModelFrom(state.sourceFileModel.contents, project, file);

                    state.editor = monaco.editor.create(editorPane, {
                        // Don't need to specify a language, because model carries this information around.
                        model: state.currentTextModel,
                        readOnly: true,
                        lineNumbers: "on",
                        scrollBeyondLastLine: true
                        }, {
                            editorService: { openEditor: openEditor },
                            //textModelService: { createModelReference: createModelReference }
                        }
                    );
                    
                    state.editor.focus();
                    if (sourceFile.span) {
                        var monacoPosition = state.currentTextModel.getPositionAt(sourceFile.span.position);

                        var position = { lineNumber: monacoPosition.lineNumber, column: monacoPosition.column };
                        state.editor.revealPositionInCenter(position);
                        state.editor.setPosition(position);
                        state.editor.deltaDecorations([],
                            [
                                {
                                    range: new monaco.Range(position.lineNumber, 1, position.lineNumber, 1),
                                    options: { className: 'highlightLine', isWholeLine: true }
                                }
                            ]);
                        state.editor.setSelection({ startLineNumber: position.lineNumber, startColumn: position.column, endLineNumber: position.lineNumber, endColumn: position.column + sourceFile.span.length });
                    }                    
            });
        }
    }
}