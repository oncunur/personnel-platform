import type { ReactNode } from "react";

export function OperationDisclosure({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: ReactNode;
}) {
  return <details className="operation-disclosure">
    <summary>
      <span className="operation-disclosure-copy">
        <strong>{title}</strong>
        <span>{description}</span>
      </span>
      <span className="operation-disclosure-action" aria-hidden="true">
        <span className="operation-disclosure-open-label">Formu aç</span>
        <span className="operation-disclosure-close-label">Formu kapat</span>
        <span className="operation-disclosure-mark">+</span>
      </span>
    </summary>
    <div className="operation-disclosure-body">{children}</div>
  </details>;
}
