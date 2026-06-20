import { A } from '@solidjs/router';
import styles from './NotFound.module.css';

/**
 * Catch-all route. Without this, an unknown URL fell through the router and
 * rendered a blank screen with no way back; here the user gets a clear message
 * and links to the two places they always want: their channels or login.
 */
export default function NotFound() {
  return (
    <div class={styles.container}>
      <div class={styles.card}>
        <p class={styles.code}>404</p>
        <h1 class={styles.title}>Page not found</h1>
        <p class={styles.body}>
          The page you're looking for doesn't exist or may have moved.
        </p>
        <div class={styles.actions}>
          <A href="/" class={styles.primary}>Go home</A>
          <A href="/login" class={styles.secondary}>Log in</A>
        </div>
      </div>
    </div>
  );
}
