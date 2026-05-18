// @ts-check

import {themes as prismThemes} from 'prism-react-renderer';

const repository = process.env.GITHUB_REPOSITORY ?? 'invoke/invoke';
const [owner, repo] = repository.split('/');
const organizationName = process.env.GITHUB_OWNER ?? owner ?? 'invoke';
const projectName = repo ?? 'invoke';
const isUserSite =
  projectName.toLowerCase() === `${organizationName.toLowerCase()}.github.io`;
const url = process.env.DOCUSAURUS_URL ?? `https://${organizationName}.github.io`;
const baseUrl = isUserSite ? '/' : `/${projectName}/`;

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Invoke',
  tagline:
    'Keyboard-first Windows launcher for apps, commands, files, windows, and scripts.',
  favicon: 'img/invoke-logo.png',
  future: {
    v4: true,
  },
  url,
  baseUrl,
  organizationName,
  projectName,
  onBrokenLinks: 'throw',
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },
  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.js',
          routeBasePath: 'docs',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      },
    ],
  ],
  themeConfig: {
    image: 'img/invoke-logo.png',
    colorMode: {
      defaultMode: 'dark',
      disableSwitch: true,
      respectPrefersColorScheme: false,
    },
    navbar: {
      title: 'Invoke',
      logo: {
        alt: 'Invoke logo',
        src: 'img/invoke-logo.png',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {to: '/docs/overview', label: 'Overview', position: 'left'},
        {
          to: '/docs/getting-started/install-and-run',
          label: 'Get Started',
          position: 'left',
        },
        {
          to: '/docs/extensions/rich-scripts',
          label: 'Scripts',
          position: 'left',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {label: 'Overview', to: '/docs/overview'},
            {
              label: 'Install and run',
              to: '/docs/getting-started/install-and-run',
            },
          ],
        },
        {
          title: 'Customize',
          items: [
            {
              label: 'Configuration',
              to: '/docs/customization/configuration-reference',
            },
            {label: 'Themes', to: '/docs/customization/themes'},
          ],
        },
        {
          title: 'Extend',
          items: [
            {label: 'Rich scripts', to: '/docs/extensions/rich-scripts'},
            {label: 'Dmenu', to: '/docs/integrations/dmenu'},
          ],
        },
      ],
      copyright: `Copyright ${new Date().getFullYear()} Invoke. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.vsDark,
      darkTheme: prismThemes.vsDark,
    },
  },
};

export default config;
