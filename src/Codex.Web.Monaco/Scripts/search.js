var lastQuery = null;
var lastSearchString = "";
var searchTimerID = -1;
var searchBox = null;

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

    var link = document.getElementById("feedbackButtonLink");
    link.href = "mailto:" + "codexteam" + "@" + "microsoft" + '.' + "com";

    window.onpopstate = OnWindowPopState;
}

function ensureSearchBox() {
    if (typeof searchBox === "object" && searchBox != null) {
        return;
    }

    searchBox = document.getElementById("search-box");

    lastSearchString = searchBox.value;

    searchBox.focus();

    searchBox.onkeyup = function () {
        if (event && event.keyCode == 13) {
            lastSearchString = "";
            onSearchChange();
        }
    };

    searchBox.oninput = function () {
        onSearchChange();
    };
}

function onSearchChange() {
    if (lastSearchString && lastSearchString == searchBox.value) {
        return;
    }

    if (searchBox.value.length > 2) {
        if (searchTimerID == -1) {
            searchTimerID = setTimeout(runSearch, 200);
        }
    } else {
        setLeftPane("<div class='note'>Enter at least 3 characters.</div>");
        lastSearchString = searchBox.value;
    }
}

function LoadSearchCore(searchText) {
    if (searchBox.value != searchText || currentState.leftPaneContent != "search") {
        searchBox.value = searchText;
        searchBox.focus();
        runSearch();
    }
}

function runSearch() {
    // Reset the timerID so we can kick off the next search timeout
    searchTimerID = -1;

    // Is a search currently running? If so abort it first
    if (typeof lastQuery === "object" && lastQuery !== null) {
        lastQuery.abort();
        lastQuery = null;
    }

    // Call the SearchController to fetch the search results and render them as HTML
    lastQuery = $.ajax({
        url: codexWebRootPrefix + "/search/ResultsAsHtml",
        type: "GET",
        data: { "searchTerm": escape(searchBox.value) },
        success: function (data) {
            lastQuery = null;
            lastSearchString = searchBox.value;
            setLeftPane(data);

            if (currentState.leftPaneContent == "search") {
                if (currentState.searchText != lastSearchString) {
                    currentState.searchText = lastSearchString;
                    currentState.windowTitle = lastSearchString;
                    ReplaceCurrentState();
                }
            } else {
                var state = {
                    leftProjectId: currentState.leftProjectId,
                    rightProjectId: currentState.rightProjectId,
                    filePath: currentState.filePath,
                    leftSymbolId: currentState.leftSymbolId,
                    rightSymbolId: currentState.rightSymbolId,
                    lineNumber: currentState.lineNumber,
                    windowTitle: lastSearchString,
                    searchText: lastSearchString,
                    leftPaneContent: "search",
                    rightPaneContent: currentState.rightPaneContent,
                };
                NavigateToState(state);
            }
        },
        error: function (jqXHR, textStatus, errorThrown) {
            if (textStatus !== "abort") {
                setLeftPane(jqXHR + "\n" + textStatus + "\n" + errorThrown);
            }
        }
    });
}