import clsx from 'clsx';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import useBaseUrl from '@docusaurus/useBaseUrl';
import HomepageFeatures from '@site/src/components/HomepageFeatures';
import styles from './index.module.css';

function HeroPill({children}) {
  return <span className={styles.heroPill}>{children}</span>;
}

function HomepageHeader() {
  const logoUrl = useBaseUrl('/img/invoke-logo.png');

  return (
    <header className={clsx('hero', styles.heroBanner)}>
      <div className={styles.heroBackdrop} />
      <div className="container">
        <div className={styles.heroShell}>
          <div className={styles.heroCopy}>
            <div className={styles.pillRow}>
              <HeroPill>Windows</HeroPill>
              <HeroPill>WPF</HeroPill>
              <HeroPill>Keyboard-first</HeroPill>
              <HeroPill>Scriptable</HeroPill>
            </div>
            <Heading as="h1" className={styles.heroTitle}>
              Invoke turns one search box into your app launcher, command palette,
              file jumper, window switcher, and script front end.
            </Heading>
            <p className={styles.heroSubtitle}>
              Config-first launcher for Windows with built-in modes, rofi-style
              themes, Everything-powered file search, and rich external scripts.
            </p>
            <div className={styles.buttonRow}>
              <Link className="button button--primary button--lg" to="/docs/overview">
                Read docs
              </Link>
              <Link
                className="button button--secondary button--lg"
                to="/docs/getting-started/install-and-run">
                Install and run
              </Link>
            </div>
          </div>
          <div className={styles.heroPanel}>
            <div className={styles.heroPanelChrome}>
              <span />
              <span />
              <span />
            </div>
            <div className={styles.heroPanelBody}>
              <img
                className={styles.heroLogo}
                src={logoUrl}
                alt="Invoke logo"
              />
              <div className={styles.heroTerminal}>
                <div className={styles.heroPrompt}>Applications:</div>
                <div className={styles.heroInput}>code</div>
                <div className={styles.heroResults}>
                  <div className={styles.heroResultActive}>
                    <strong>Visual Studio Code</strong>
                    <span>App</span>
                  </div>
                  <div className={styles.heroResult}>
                    <strong>code</strong>
                    <span>Executable on PATH</span>
                  </div>
                  <div className={styles.heroResult}>
                    <strong>Invoke Docs</strong>
                    <span>Rich script result</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
}

export default function Home() {
  return (
    <Layout
      title="Invoke documentation"
      description="Documentation for Invoke, keyboard-first Windows launcher for apps, commands, files, windows, and rich scripts.">
      <HomepageHeader />
      <main>
        <HomepageFeatures />
      </main>
    </Layout>
  );
}
