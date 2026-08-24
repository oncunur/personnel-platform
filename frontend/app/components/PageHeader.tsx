import type { ReactNode } from "react";

export function PageHeader({
  eyebrow,
  title,
  description,
  status,
  actions,
}: {
  eyebrow: string;
  title: string;
  description: string;
  status?: string;
  actions?: ReactNode;
}) {
  return <>
    <header className="page-header">
      <div className="page-heading">
        <span className="page-eyebrow">{eyebrow}</span>
        <h1 className="page-title">{title}</h1>
        <p className="page-subtitle">{description}</p>
      </div>
      {actions ? <div className="page-actions">{actions}</div> : null}
    </header>
    {status ? <div className="message-strip" role="status" aria-live="polite">{status}</div> : null}
  </>;
}
