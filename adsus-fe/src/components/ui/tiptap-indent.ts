import { Extension } from '@tiptap/core';

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    indent: {
      indent: () => ReturnType;
      outdent: () => ReturnType;
    };
  }
}

export const Indent = Extension.create({
  name: 'indent',

  addOptions() {
    return {
      types: ['heading', 'paragraph'],
    };
  },

  addGlobalAttributes() {
    return [
      {
        types: this.options.types,
        attributes: {
          indent: {
            default: 0,
            parseHTML: element => {
              const identAttr = element.getAttribute('data-indent');
              return identAttr ? parseInt(identAttr, 10) : 0;
            },
            renderHTML: attributes => {
              if (!attributes.indent) {
                return {};
              }
              return {
                'data-indent': attributes.indent,
                style: `margin-left: ${attributes.indent}px;`,
              };
            },
          },
        },
      },
    ];
  },

  addCommands() {
    return {
      indent: () => ({ tr, state, dispatch }) => {
        const { selection } = state;
        tr.doc.nodesBetween(selection.from, selection.to, (node, pos) => {
          if (this.options.types.includes(node.type.name)) {
            let indent = node.attrs.indent || 0;
            if (indent < 200) indent += 20;
            if (dispatch) {
              tr.setNodeMarkup(pos, undefined, { ...node.attrs, indent });
            }
          }
        });
        if (dispatch) dispatch(tr);
        return true;
      },
      outdent: () => ({ tr, state, dispatch }) => {
        const { selection } = state;
        tr.doc.nodesBetween(selection.from, selection.to, (node, pos) => {
          if (this.options.types.includes(node.type.name)) {
            let indent = node.attrs.indent || 0;
            if (indent > 0) indent -= 20;
            if (dispatch) {
              tr.setNodeMarkup(pos, undefined, { ...node.attrs, indent });
            }
          }
        });
        if (dispatch) dispatch(tr);
        return true;
      },
    };
  },
});
