/// <reference path="../node_modules/@types/jquery/index.d.ts"/>
function serverWithPrefix(url) {
    return server(state.codexWebRootPrefix + url);
}
function server(url) {
    return new Promise(function (resolve, reject) {
        $.ajax({
            url: url,
            type: "GET",
            success: function (data) { resolve(data); return data; },
            error: function (jqXHR, textStatus, errorThrown) {
                if (textStatus !== "abort") {
                    //errorCallback(jqXHR + "\n" + textStatus + "\n" + errorThrown);
                    reject(new Error(jqXHR + "\n" + textStatus + "\n" + errorThrown));
                }
            }
        });
    });
}
function getReference(_this, position) {
    var segmentIndex = ~~(position / _this.segmentLength);
    if (segmentIndex >= _this.segments.length) {
        return undefined;
    }
    var segment = _this.segments[segmentIndex];
    if (!segment) {
        return undefined;
    }
    for (var _i = 0, _a = segment.references; _i < _a.length; _i++) {
        var symbolSpan = _a[_i];
        if (position >= symbolSpan.span.position &&
            position <= (symbolSpan.span.position + symbolSpan.span.length)) {
            return symbolSpan;
        }
    }
    return undefined;
}
function getDefinition(_this, position) {
    var segmentIndex = ~~(position / _this.segmentLength);
    if (segmentIndex >= _this.segments.length) {
        return undefined;
    }
    var segment = _this.segments[segmentIndex];
    if (!segment) {
        return undefined;
    }
    for (var _i = 0, _a = segment.definitions; _i < _a.length; _i++) {
        var symbolSpan = _a[_i];
        if (position >= symbolSpan.span.position &&
            position <= (symbolSpan.span.position + symbolSpan.span.length)) {
            return symbolSpan;
        }
    }
    return undefined;
}
function getDefinitionForSymbol(_this, symbolId) {
    if (!_this.segments) {
        return undefined;
    }
    for (var _i = 0, _a = _this.segments; _i < _a.length; _i++) {
        var segment = _a[_i];
        for (var _b = 0, _c = segment.definitions; _b < _c.length; _b++) {
            var symbolSpan = _c[_b];
            if (symbolSpan.symbol === symbolId) {
                return symbolSpan;
            }
        }
    }
    return undefined;
}
function getSourceFileContents(projectId, filePath) {
    var url = "/sourcecontent/" + encodeURI(projectId) + "/?filename=" + encodeURI(filePath);
    return serverWithPrefix(url);
}
function getFindAllReferencesHtml(projectId, symbolId, projectScope) {
    var url = "/references/" + encodeURI(projectId) + "/?symbolId=" + encodeURI(symbolId);
    if (projectScope) {
        url += "&projectScope=" + encodeURI(projectScope);
    }
    return serverWithPrefix(url);
}
function getDefinitionLocation(projectId, symbol) {
    var url = "/definitionscontents/" + encodeURI(projectId) + "/?symbolId=" + encodeURI(symbol);
    return serverWithPrefix(url);
}
