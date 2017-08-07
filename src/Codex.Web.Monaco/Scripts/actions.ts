/// <reference path="../node_modules/@types/jquery/index.d.ts"/>

// High level functions for use by result elements to control web page state

namespace Actions {
    export function GoToDef(projectId: string, filePath: string, symbolId: string, repoId?: string) : void {
        state.WebPage.setViewModel({
            right: <FileViewModel>{
                kind: 'SourceFile',
                filePath: filePath,
                projectId: projectId,
                targetLocation: {
                    kind: 'symbol', value: { projectId: projectId, symbolId: symbolId }
                }
            },
            left: undefined
        });
    }
}