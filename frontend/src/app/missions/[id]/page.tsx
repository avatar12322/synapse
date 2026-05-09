import MissionDetailClient from './MissionDetailClient';

// Empty list — Capacitor handles dynamic routing natively at runtime
export function generateStaticParams() {
  return [];
}

export default function MissionDetailPage() {
  return <MissionDetailClient />;
}
