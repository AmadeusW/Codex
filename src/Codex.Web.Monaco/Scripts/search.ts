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

    // onkeydown supports repeating when user holds the button
    state.searchBox.onkeydown = function (event) {
        if (event) {
            switch (event.keyCode) {
                case 38: // up
                    if (event.ctrlKey) {
                        collapseResult();
                    } else {
                        selectPreviousResult();
                    }
                    return false; // cancel the event so the caret doesn't move
                case 40: // down
                    if (event.ctrlKey) {
                        expandResult();
                    } else {
                        selectNextResult();
                    }
                    return false; // cancel the event so the caret doesn't move
                default:
                    break;
            }
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
    resetSelectedResult();

    if (state.searchBox.value.length > 2) {
        if (state.searchTimerID == -1) {
            state.searchTimerID = setTimeout(runSearch, 200);
        }
    } else {
        setLeftPane("<div class='note'>Enter at least 3 characters.</div>");
        state.lastSearchString = state.searchBox.value;
    }
}

// Called when a new search happens and the result list is rebuilt
function resetSelectedResult() {
    state.keyboardSelectedResultGroup = null;
    state.keyboardSelectedResult = null;
    if (state.keyboardSelectedUIElement != null) {
        state.keyboardSelectedUIElement.removeClass("selectedResult");
        state.keyboardSelectedUIElement = null;
    }
}

function expandResult() {
    if (state.keyboardSelectedUIElement == null || !state.keyboardSelectedUIElement.length) {
        return;
    }
    if (state.keyboardSelectedUIElement.is('.resultItem')) { // Selected item is a single result
        state.keyboardSelectedUIElement.parent().trigger("click");
    } else if (state.keyboardSelectedUIElement.is('.resultGroupHeader')) { // Selected item is a group
        if (state.keyboardSelectedUIElement.next("div").css("display") == "none") {
            state.keyboardSelectedUIElement.trigger("click");
        }
    }
}

function collapseResult() {
    if (state.keyboardSelectedUIElement == null || !state.keyboardSelectedUIElement.length) {
        return;
    }
    if (state.keyboardSelectedUIElement.is('.resultItem')) { // Selected item is a single result
        // Select the parent group and collapse it
        state.keyboardSelectedUIElement.removeClass("selectedResult");
        state.keyboardSelectedResult = null;
        state.keyboardSelectedResultGroup = state.keyboardSelectedUIElement.parent().parent().parent().first();
        state.keyboardSelectedUIElement = state.keyboardSelectedResultGroup.children('.resultGroupHeader')
        state.keyboardSelectedUIElement.trigger("click");
        state.keyboardSelectedUIElement.addClass("selectedResult");
    } else if (state.keyboardSelectedUIElement.is('.resultGroupHeader')) { // Selected item is a group
        if (state.keyboardSelectedUIElement.next("div").css("display") != "none") {
            state.keyboardSelectedUIElement.trigger("click");
        }
    }
}

function selectNextResult() {
    if (state.keyboardSelectedUIElement != null) { // Deselect currently selected element
        state.keyboardSelectedUIElement.removeClass("selectedResult");
    }

    if (state.keyboardSelectedResultGroup == null || !state.keyboardSelectedResultGroup.length) { // Nothing is selected. Select the first group.
        state.keyboardSelectedResultGroup = $(".resultGroup").first();
        state.keyboardSelectedUIElement = state.keyboardSelectedResultGroup.children(".resultGroupHeader").first();
    }
    else {
        if (state.keyboardSelectedResultGroup.children().not(".resultGroupHeader").first().css("display") != "none") {
            if (state.keyboardSelectedResult == null || !state.keyboardSelectedResult.length) { // Select the first result
                var resultContainer = state.keyboardSelectedResultGroup.children().not(".resultGroupHeader").first();
                var resultLink = resultContainer.children("a").first();
                state.keyboardSelectedResult = resultLink.children(".resultItem").first();
                state.keyboardSelectedUIElement = state.keyboardSelectedResult;
            }
            else { // Select the next result
                var resultLink = state.keyboardSelectedResult.parent().next();
                if (resultLink.is('a')) {
                    state.keyboardSelectedResult = resultLink.children(".resultItem").first();
                    state.keyboardSelectedUIElement = state.keyboardSelectedResult;
                }
                else { // There are no more siblings. Go to the next group
                    state.keyboardSelectedResult = null;
                    state.keyboardSelectedResultGroup = state.keyboardSelectedResultGroup.next(".resultGroup");
                    if (state.keyboardSelectedResultGroup.is("div")) {
                        state.keyboardSelectedUIElement = state.keyboardSelectedResultGroup.children(".resultGroupHeader").first();
                    }
                    else {
                        // We've reached the end of the list
                        state.keyboardSelectedUIElement = null;
                        return;
                    }
                }
            }
        }
        else { // Select the next group
            state.keyboardSelectedResult = null; // so that we try to go back to a result
            state.keyboardSelectedResultGroup = state.keyboardSelectedResultGroup.next(".resultGroup");
            if (state.keyboardSelectedResultGroup.is("div")) {
                state.keyboardSelectedUIElement = state.keyboardSelectedResultGroup.children(".resultGroupHeader").first();
            }
            else {
                // We've reached the end of the list
                state.keyboardSelectedUIElement = null;
                return;
            }
        }
    }
    actOnSelectedResult();
}

function selectPreviousResult() {
    if (state.keyboardSelectedUIElement != null) {
        state.keyboardSelectedUIElement.removeClass("selectedResult");
    }

    if (state.keyboardSelectedResult == null || !state.keyboardSelectedResult.length) {
        // Last result
        state.keyboardSelectedResultGroup = state.keyboardSelectedResultGroup.prev(".resultGroup");
        if (state.keyboardSelectedResultGroup.children().not(".resultGroupHeader").first().css("display") != "none") {
            if (state.keyboardSelectedResultGroup.is("div")) {
                var resultContainer = state.keyboardSelectedResultGroup.children().not(".resultGroupHeader").first();
                var resultLink = resultContainer.children("a").last();
                state.keyboardSelectedResult = resultLink.children(".resultItem").first();
                state.keyboardSelectedUIElement = state.keyboardSelectedResult;
            }
            else {
                // We've reached the beginning of the list
                state.keyboardSelectedResult = null;
                state.keyboardSelectedUIElement = null;
                $("#leftPane").scrollTop(0); // scroll to the very top
                return;
            }
        }
        else {
            // The group we would have went to is collapsed. go to its header
            state.keyboardSelectedResult = null;
            state.keyboardSelectedUIElement = state.keyboardSelectedResultGroup.children(".resultGroupHeader").first();
        }
    }
    else {
        var resultLink = state.keyboardSelectedResult.parent().prev();
        if (resultLink.is('a')) {
            // Previous result
            state.keyboardSelectedResult = resultLink.children(".resultItem").first();
            state.keyboardSelectedUIElement = state.keyboardSelectedResult;
        }
        else { // There are no more siblings. Go to the previous group
            state.keyboardSelectedResult = null; // so that we try to go back to a result
            if (state.keyboardSelectedResultGroup.is("div")) {
                state.keyboardSelectedUIElement = state.keyboardSelectedResultGroup.children(".resultGroupHeader").first();
            }
            else {
                // We've reached the beginning
                state.keyboardSelectedUIElement = null;
                $("#leftPane").scrollTop(0); // scroll to the very top
                return;
            }
        }
    }
    actOnSelectedResult();
}

function actOnSelectedResult() {
    if (state.keyboardSelectedUIElement != null && state.keyboardSelectedUIElement.length) {
        // Visually select the element
        state.keyboardSelectedUIElement.addClass("selectedResult");

        // Scroll to the element. Not all browsers support scrollIntoViewIfNeeded()
        if (typeof state.keyboardSelectedUIElement[0].scrollIntoViewIfNeeded === "function") {
            state.keyboardSelectedUIElement[0].scrollIntoViewIfNeeded();
        } else {
            state.keyboardSelectedUIElement[0].scrollIntoView();
        }
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