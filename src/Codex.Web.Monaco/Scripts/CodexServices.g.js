/// <reference path="..\\node_modules\\monaco-editor\\monaco.d.ts" />
var CodexSymbolProvider = (function () {
    function CodexSymbolProvider() {
    }
    /**
     * Provide symbol information for the given document.
     */
    CodexSymbolProvider.prototype.provideDocumentSymbols = function (model, token) {
        return [];
    };
    return CodexSymbolProvider;
}());
var CodexTokensProvider = (function () {
    function CodexTokensProvider() {
    }
    CodexTokensProvider.prototype.getInitialState = function () {
        return undefined;
    };
    CodexTokensProvider.prototype.tokenize = function (line, state) {
        return undefined;
    };
    return CodexTokensProvider;
}());
function registerMonacoServices() {
    monaco.languages.register({
        id: "codex"
    });
    monaco.languages.onLanguage("codex", function () {
        console.log("hello codex");
    });
    //monaco.languages.setTokensProvider("codex", new CodexTokensProvider());
    monaco.languages.registerDocumentSymbolProvider("codex", new CodexSymbolProvider());
}
//# sourceMappingURL=CodexServices.g.js.map