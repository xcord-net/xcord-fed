import styles from './Home.module.css';

export default function Home() {
  return (
    <div data-testid="home-page" class={styles.pageWrapper}>
      <p data-testid="home-placeholder" class={styles.placeholder}>Xcord client - channels coming soon</p>
    </div>
  );
}
