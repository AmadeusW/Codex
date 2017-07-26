/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

function serverWithPrefix<T>(url: string): Promise<T> {
    return server(codexWebRootPrefix + url);
}

function server<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) => {
        $.ajax({
            url: url,
            type: "GET",
            success: data => { resolve(data); return <T>data; },
            error: function (jqXHR, textStatus, errorThrown) {
                if (textStatus !== "abort") {
                    //errorCallback(jqXHR + "\n" + textStatus + "\n" + errorThrown);
                    reject(new Error(jqXHR + "\n" + textStatus + "\n" + errorThrown));
                }
            }
        });
    });
}

interface Span {
    position: number;
    length: number;
}

interface SymbolSpan {
    symbol: string;
    projectId: string;
    span: Span;
}

interface SegmentModel {
    definitions: SymbolSpan[];
    references: SymbolSpan[];
}

interface SourceFileContentsModel {
    filePath: string;
    webLink: string;
    repoRelativePath: string;
    projectId: string;

    contents: string;
    span: Span;
    segments: SegmentModel[];
    // Width int characters of segments
    segmentLength: number;
}

function getReference(_this: SourceFileContentsModel, position: number): SymbolSpan {
    let segmentIndex = ~~(position / _this.segmentLength);
    if (segmentIndex >= _this.segments.length) {
        return undefined;
    }

    let segment = _this.segments[segmentIndex];
    if (!segment) {
        return undefined;
    }

    for (let symbolSpan of segment.references) {
        if (position >= symbolSpan.span.position &&
            position <= (symbolSpan.span.position + symbolSpan.span.length)) {
            return symbolSpan;
        }
    }

    return undefined;
}

function getDefinition(_this: SourceFileContentsModel, position: number): SymbolSpan {
    let segmentIndex = ~~(position / _this.segmentLength);
    if (segmentIndex >= _this.segments.length) {
        return undefined;
    }

    let segment = _this.segments[segmentIndex];
    if (!segment) {
        return undefined;
    }

    for (let symbolSpan of segment.definitions) {
        if (position >= symbolSpan.span.position &&
            position <= (symbolSpan.span.position + symbolSpan.span.length)) {
            return symbolSpan;
        }
    }

    return undefined;
}

type DefinitionLocation = string | SourceFileContentsModel;

function getSourceFileContents(projectId: string, filePath: string): SourceFileContentsModel {
    return undefined;
}

function getFindAllReferencesHtml(projectId: string, symbolId: string): string {
    return null;
}

function getDefinitionLocation(projectId: string, symbol: string): Promise<DefinitionLocation> {
    let url = `/definitionscontents/${encodeURI(projectId)}/?symbolId=${encodeURI(symbol)}`;
    return serverWithPrefix<DefinitionLocation>(url);
}





























