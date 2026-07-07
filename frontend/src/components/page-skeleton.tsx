export function PageSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-4">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="h-24 rounded-xl bg-zinc-900 animate-pulse" />
      ))}
    </div>
  );
}
