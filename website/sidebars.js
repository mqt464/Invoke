// @ts-check

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    'overview',
    {
      type: 'category',
      label: 'Getting started',
      items: [
        'getting-started/install-and-run',
        'getting-started/first-configuration',
      ],
    },
    {
      type: 'category',
      label: 'Usage',
      items: ['usage/modes', 'usage/keyboard-and-hotkeys'],
    },
    {
      type: 'category',
      label: 'Customization',
      items: [
        'customization/configuration-reference',
        'customization/themes',
      ],
    },
    {
      type: 'category',
      label: 'Extensions',
      items: ['extensions/rich-scripts', 'integrations/dmenu'],
    },
    {
      type: 'category',
      label: 'Reference',
      items: ['reference/architecture', 'reference/deploy-docs'],
    },
  ],
};

export default sidebars;
