import Heading from '@theme/Heading';
import styles from './styles.module.css';

const FeatureList = [
  {
    title: 'Built for daily launcher flow',
    body:
      'Use one surface for app launch, PATH commands, file jump, open window switching, and multi-provider combi search.',
  },
  {
    title: 'Config files, not hidden settings',
    body:
      'Invoke seeds config.rasi, default.rasi, and script folders so you can version, diff, and tune behavior directly.',
  },
  {
    title: 'Script modes with real actions',
    body:
      'Custom scripts can return scored rows, prompt text, query rewrites, URL actions, path actions, and persistent sessions.',
  },
  {
    title: 'Rofi-style theming on Windows',
    body:
      'Theme blocks like window, inputbar, listview, and element selected map into live WPF presentation.',
  },
  {
    title: 'Live reload loop',
    body:
      'Config, theme, and script changes trigger runtime rebuilds without forcing full app restart.',
  },
  {
    title: 'Everything and dmenu friendly',
    body:
      'Files mode uses Everything for speed, and Invoke.Cli can drive Invoke.App as graphical dmenu session.',
  },
];

function FeatureCard({title, body, index}) {
  return (
    <article className={styles.card}>
      <div className={styles.cardIndex}>{String(index + 1).padStart(2, '0')}</div>
      <Heading as="h3" className={styles.cardTitle}>
        {title}
      </Heading>
      <p className={styles.cardBody}>{body}</p>
    </article>
  );
}

export default function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className={styles.sectionLead}>
          <span className={styles.kicker}>Documentation map</span>
          <Heading as="h2" className={styles.heading}>
            Practical docs for operating, extending, and skinning Invoke.
          </Heading>
          <p className={styles.copy}>
            Start at install, learn built-in modes, wire rich scripts, then shape
            launcher with `.rasi` themes and per-mode config.
          </p>
        </div>
        <div className={styles.grid}>
          {FeatureList.map((feature, idx) => (
            <FeatureCard
              key={feature.title}
              title={feature.title}
              body={feature.body}
              index={idx}
            />
          ))}
        </div>
      </div>
    </section>
  );
}
