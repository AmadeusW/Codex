/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

module rpc {
    function serverWithPrefix<T>(url: string): Promise<T> {
        return server(state.codexWebRootPrefix + url);
    }

    export function server<T>(url: string): Promise<T> {
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

    export function getReference(_this: SourceFile, position: number): SymbolSpan {
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

    export function getDefinition(_this: SourceFile, position: number): SymbolSpan {
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

    export function getDefinitionForSymbol(sourceFile: SourceFile, symbolId: string): SymbolSpan {
        if (!sourceFile.segments) {
            return undefined;
        }

        for (let segment of sourceFile.segments) {
            for (let symbolSpan of segment.definitions) {
                if (symbolSpan.symbol === symbolId) {
                    return symbolSpan;
                }
            }
        }

        return undefined;
    }

    export function getSourceFileContents(projectId: string, filePath: string): Promise<SourceFile> {
        let url = `/sourcecontent/${encodeURI(projectId)}/?filename=${encodeURI(filePath)}`;
        return serverWithPrefix<SourceFile>(url);
    }

    export function getFindAllReferencesHtml(projectId: string, symbolId: string, projectScope?: string): Promise<string> {
        let url = `/references/${encodeURI(projectId)}/?symbolId=${encodeURI(symbolId)}`;
        if (projectScope) {
            url += `&projectScope=${encodeURI(projectScope)}`;
        }

        return serverWithPrefix<string>(url);
    }

    export function getDefinitionLocation(projectId: string, symbol: string): Promise<SourceFileOrView> {
        let url = `/definitionscontents/${encodeURI(projectId)}/?symbolId=${encodeURI(symbol)}`;
        return serverWithPrefix<SourceFileOrView>(url);
    }

    export function getToolTip(projectId: string, symbol: string): Promise<ToolTip> {
        let url = `/tooltip/${encodeURI(projectId)}/?symbolId=${encodeURI(symbol)}`;
        return serverWithPrefix<ToolTip>(url);
    }
}