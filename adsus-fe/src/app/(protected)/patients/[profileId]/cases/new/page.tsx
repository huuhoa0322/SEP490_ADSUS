import { redirect } from "next/navigation";

export default async function CreateCasePage({
  params,
}: {
  params: Promise<{ profileId: string }>;
}) {
  const { profileId } = await params;
  redirect(`/patients/${profileId}`);
}
