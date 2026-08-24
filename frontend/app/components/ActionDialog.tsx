"use client";

import { FormEvent, useEffect, useId, useRef, useState } from "react";

export type ActionDialogField = {
  name: string;
  label: string;
  initialValue?: string;
  type?: "text" | "number" | "date";
  required?: boolean;
  min?: string | number;
  max?: string | number;
  step?: string | number;
  placeholder?: string;
  helpText?: string;
  multiline?: boolean;
};

export type ActionDialogOptions = {
  title: string;
  description: string;
  confirmLabel: string;
  cancelLabel?: string;
  tone?: "default" | "danger" | "success";
  fields?: ActionDialogField[];
};

type DialogResult = Record<string, string> | null;

export function useActionDialog() {
  const [options, setOptions] = useState<ActionDialogOptions | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const resolver = useRef<((result: DialogResult) => void) | null>(null);
  const returnFocus = useRef<HTMLElement | null>(null);

  useEffect(() => () => resolver.current?.(null), []);

  function ask(next: ActionDialogOptions): Promise<DialogResult> {
    resolver.current?.(null);
    returnFocus.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setValues(Object.fromEntries((next.fields ?? []).map(field => [field.name, field.initialValue ?? ""])));
    setOptions(next);
    return new Promise(resolve => { resolver.current = resolve; });
  }

  function finish(result: DialogResult) {
    const resolve = resolver.current;
    resolver.current = null;
    setOptions(null);
    resolve?.(result);
    window.requestAnimationFrame(() => returnFocus.current?.focus());
  }

  const dialog = options ? <ActionDialog
    options={options}
    values={values}
    onChange={(name, value) => setValues(current => ({ ...current, [name]: value }))}
    onConfirm={() => finish(values)}
    onCancel={() => finish(null)}
  /> : null;

  return { ask, dialog };
}

function ActionDialog({ options, values, onChange, onConfirm, onCancel }: {
  options: ActionDialogOptions;
  values: Record<string, string>;
  onChange: (name: string, value: string) => void;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const node = dialogRef.current;
    if (node && !node.open) node.showModal();
  }, []);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onConfirm();
  }

  return <dialog
    ref={dialogRef}
    className="action-dialog"
    aria-labelledby={titleId}
    aria-describedby={descriptionId}
    onCancel={event => { event.preventDefault(); onCancel(); }}
    onMouseDown={event => { if (event.target === event.currentTarget) onCancel(); }}
  >
    <form className="action-dialog-form" onSubmit={submit}>
      <div className="action-dialog-heading">
        <span className="eyebrow dark">İşlem onayı</span>
        <h2 id={titleId}>{options.title}</h2>
        <p id={descriptionId}>{options.description}</p>
      </div>
      {options.fields?.length ? <div className="action-dialog-fields">
        {options.fields.map(field => <label className="field-label" key={field.name}>
          {field.label}
          {field.multiline ? <textarea
            value={values[field.name] ?? ""}
            onChange={event => onChange(field.name, event.target.value)}
            required={field.required}
            placeholder={field.placeholder}
            rows={4}
          /> : <input
            type={field.type ?? "text"}
            value={values[field.name] ?? ""}
            onChange={event => onChange(field.name, event.target.value)}
            required={field.required}
            min={field.min}
            max={field.max}
            step={field.step}
            placeholder={field.placeholder}
          />}
          {field.helpText ? <small>{field.helpText}</small> : null}
        </label>)}
      </div> : null}
      <div className="action-dialog-actions">
        <button className="secondary-button" type="button" onClick={onCancel}>{options.cancelLabel ?? "Vazgeç"}</button>
        <button className={`primary-button ${options.tone === "danger" ? "button-danger" : options.tone === "success" ? "button-success" : ""}`} type="submit">{options.confirmLabel}</button>
      </div>
    </form>
  </dialog>;
}
