import { EmailComposer } from '../components/EmailComposer';

export function EmailsPage() {
  return (
    <div>
      <h1 className="text-2xl font-bold text-zinc-100 mb-6">Submit Email</h1>
      <EmailComposer />
    </div>
  );
}
