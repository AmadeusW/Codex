/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
/// <reference path="../node_modules/monaco-editor/monaco.d.ts"/>
/// <reference path="rpc.ts"/>
/// <reference path="editor.ts"/>
/// <reference path="state.ts"/>

declare function Split(x: any, y: any);
declare function escape(x: any);

function onBodyLoad() {

    var anchor = document.location.hash;
    if (anchor && !document.location.search && document.location.pathname === "/") {
        top.location.replace("http://ddindex/" + anchor);
        return;
    }

    // https://github.com/nathancahill/Split.js
    Split(['#leftPane', '#rightPane'], {
        sizes: ['504px', 'calc(100% - 504px)'],
        gutterSize: 20,
        minSize: 1,
        cursor: 'col-resize'
    });

    ensureSearchBox();

    // TODO: why href is missing?
    var link = <any>document.getElementById("feedbackButtonLink");
    link.href = "mailto:" + "codexteam" + "@" + "microsoft" + '.' + "com";

    window.onpopstate = OnWindowPopState;
}

function ensureSearchBox() {
    if (typeof state.searchBox === "object" && state.searchBox != null) {
        return;
    }

    state.searchBox = document.getElementById("search-box");

    state.lastSearchString = state.searchBox.value;

    state.searchBox.focus();

    state.searchBox.onkeyup = function (event) {
        if (event && (<any>event).keyCode == 13) {
            state.lastSearchString = "";
            onSearchChange();
        }
    };

    state.searchBox.oninput = function () {
        onSearchChange();
    };
}

function onSearchChange() {
    if (state.lastSearchString && state.lastSearchString === state.searchBox.value) {
        return;
    }

    if (state.searchBox.value.length > 2) {
        if (state.searchTimerID == -1) {
            state.searchTimerID = setTimeout(runSearch, 200);
        }
    } else {
        setLeftPane("<div class='note'>Enter at least 3 characters.</div>");
        state.lastSearchString = state.searchBox.value;
    }
}

function LoadSearchCore(searchText) {
    if (state.searchBox.value != searchText || state.currentState.leftPaneContent != "search") {
        state.searchBox.value = searchText;
        state.searchBox.focus();
        runSearch();
    }
}

function runSearch() {
    // Reset the timerID so we can kick off the next search timeout
    state.searchTimerID = -1;

    // Is a search currently running? If so abort it first
    if (typeof state.lastQuery === "object" && state.lastQuery !== null) {
        (<any>state.lastQuery).abort();
        state.lastQuery = null;
    }

    // Call the SearchController to fetch the search results and render them as HTML
    state.lastQuery = <any>$.ajax({
        url: state.codexWebRootPrefix + "/search/ResultsAsHtml",
        type: "GET",
        data: { "searchTerm": escape(state.searchBox.value) },
        success: function (data) {
            state.lastQuery = null;
            state.lastSearchString = state.searchBox.value;
            setLeftPane(data);

            if (state.currentState.leftPaneContent == "search") {
                if (state.currentState.searchText != state.lastSearchString) {
                    state.currentState.searchText = state.lastSearchString;
                    state.currentState.windowTitle = state.lastSearchString;
                    ReplaceCurrentState();
                }
            } else {
                var newState = {
                    leftProjectId: state.currentState.leftProjectId,
                    rightProjectId: state.currentState.rightProjectId,
                    filePath: state.currentState.filePath,
                    leftSymbolId: state.currentState.leftSymbolId,
                    rightSymbolId: state.currentState.rightSymbolId,
                    lineNumber: state.currentState.lineNumber,
                    windowTitle: state.lastSearchString,
                    searchText: state.lastSearchString,
                    leftPaneContent: "search",
                    rightPaneContent: state.currentState.rightPaneContent,
                };
                NavigateToState(newState);
            }
        },
        error: function (jqXHR, textStatus, errorThrown) {
            if (textStatus !== "abort") {
                setLeftPane(jqXHR + "\n" + textStatus + "\n" + errorThrown);
            }
        }
    });
}